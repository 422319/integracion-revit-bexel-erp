using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Revit.DB.IFC;

namespace RevitFamilyCleaner
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Exportar primero a IFC (con StoreIFCGUID activado), así Revit graba
            // en cada elemento el GlobalId REAL que va a tener en el archivo IFC
            string rutaIFC = ExportarIFC(doc);

            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            // Construir la lista de elementos de todo el modelo
            List<MuroItem> listaMuros = new List<MuroItem>();
            foreach (Element elem in collector)
            {
                if (elem.Category == null || !elem.Category.AllowsBoundParameters) continue;

                string nombreElemento = elem.Name;
                ElementId idTipo = elem.GetTypeId();
                if (idTipo != ElementId.InvalidElementId)
                {
                    ElementType tipoElemento = doc.GetElement(idTipo) as ElementType;
                    if (tipoElemento != null && !string.IsNullOrEmpty(tipoElemento.FamilyName))
                    {
                        nombreElemento = $"{tipoElemento.FamilyName} - {tipoElemento.Name}";
                    }
                }

                string disciplinaActual = elem.LookupParameter("DISCIPLINA")?.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(disciplinaActual)) continue;

                string subDisciplina1Actual = elem.LookupParameter("SUB-DISCIPLINA 1")?.AsString() ?? "";
                string subDisciplina2Actual = elem.LookupParameter("SUB-DISCIPLINA 2")?.AsString() ?? "";

                double area = ObtenerAreaGenerica(elem);
                double volumen = ObtenerVolumenGenerico(elem);

                string unidadGuardada = ClassificationStorage.GetUnidad(elem);
                string unidadSugerida;
                if (!string.IsNullOrWhiteSpace(unidadGuardada))
                {
                    unidadSugerida = unidadGuardada;
                }
                else if (volumen > 0) unidadSugerida = "M3";
                else if (area > 0) unidadSugerida = "M2";
                else unidadSugerida = "UND";

                listaMuros.Add(new MuroItem
                {
                    ElementIdValue = elem.Id.Value,
                    Nombre = nombreElemento,
                    Categoria = elem.Category.Name,
                    GlobalId = elem.LookupParameter("IfcGUID")?.AsString() ?? "",
                    Area = area,
                    Volumen = volumen,
                    Unidad = unidadSugerida,
                    Seleccionado = false,
                    Columna1 = disciplinaActual,
                    Columna2 = subDisciplina1Actual,
                    Columna3 = subDisciplina2Actual
                });
            }

            if (listaMuros.Count == 0)
            {
                TaskDialog.Show("Sincronización", "No se encontraron elementos en el modelo.");
                return Result.Succeeded;
            }

            // Ordenar por Nombre para que los elementos iguales/similares queden agrupados
            listaMuros = listaMuros.OrderBy(m => m.Nombre).ToList();

            // Mostrar la ventana de selección
            SeleccionMurosWindow ventana = new SeleccionMurosWindow(listaMuros, uidoc);
            bool? resultado = ventana.ShowDialog();

            if (resultado != true)
            {
                // El usuario cerró la ventana sin confirmar
                return Result.Cancelled;
            }

            // Guardar Disciplina / Sub-Disciplina 1 / Sub-Disciplina 2 en los parámetros de Revit
            // (aplica a TODOS los muros de la tabla, tanto si es "Guardar" como "Enviar seleccionados")
            using (Transaction t = new Transaction(doc, "Asignar disciplina a muros"))
            {
                t.Start();

                foreach (MuroItem muro in ventana.Muros)
                {
                    Element elem = doc.GetElement(new ElementId((long)muro.ElementIdValue));
                    if (elem == null) continue;

                    Parameter paramDisciplina = elem.LookupParameter("DISCIPLINA");
                    if (paramDisciplina != null && !paramDisciplina.IsReadOnly)
                    {
                        paramDisciplina.Set(muro.Columna1 ?? "");
                    }

                    Parameter paramSubDisciplina1 = elem.LookupParameter("SUB-DISCIPLINA 1");
                    if (paramSubDisciplina1 != null && !paramSubDisciplina1.IsReadOnly)
                    {
                        paramSubDisciplina1.Set(muro.Columna2 ?? "");
                    }

                    Parameter paramSubDisciplina2 = elem.LookupParameter("SUB-DISCIPLINA 2");
                    if (paramSubDisciplina2 != null && !paramSubDisciplina2.IsReadOnly)
                    {
                        paramSubDisciplina2.Set(muro.Columna3 ?? "");
                    }

                    ClassificationStorage.SetUnidad(elem, muro.Unidad);
                }

                t.Commit();
            }

            // Si solo se presionó "Guardar", no se toca el backend
            if (ventana.SoloGuardar)
            {
                TaskDialog.Show("Guardado", $"Se guardaron los datos de {ventana.Muros.Count} muros en Revit.");
                return Result.Succeeded;
            }

            // Enviar solo los muros seleccionados
            LimpiarBackend();

            int enviados = 0;
            foreach (MuroItem muro in ventana.Seleccionados)
            {
                EnviarMedicion(muro);
                enviados++;
            }

            // Subir el modelo IFC que ya se exportó al inicio del comando
            SubirIFC(rutaIFC);

            TaskDialog.Show("Sincronización", $"Se enviaron {enviados} elementos al backend.");

            return Result.Succeeded;
        }

        private void LimpiarBackend()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    Task.Run(async () =>
                        await client.DeleteAsync("http://localhost:3000/mediciones")
                    ).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error de conexión", $"No se pudo limpiar el backend: {ex.Message}");
            }
        }

        // Intenta obtener el área de cualquier elemento, probando primero el parámetro
        // nativo de anfitrión (muros/pisos/techos) y luego el parámetro genérico "Área"
        private double ObtenerAreaGenerica(Element elem)
        {
            double interno = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0;
            if (interno == 0)
            {
                interno = elem.LookupParameter("Área")?.AsDouble() ?? 0;
            }
            return interno == 0 ? 0 : Math.Round(UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.SquareMeters), 3);
        }

        // Igual que ObtenerAreaGenerica, pero para volumen
        private double ObtenerVolumenGenerico(Element elem)
        {
            double interno = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            if (interno == 0)
            {
                interno = elem.LookupParameter("Volumen")?.AsDouble() ?? 0;
            }
            return interno == 0 ? 0 : Math.Round(UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.CubicMeters), 3);
        }

        private void EnviarMedicion(MuroItem muro)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var data = new
                    {
                        nombre_elemento = muro.Nombre,
                        categoria = muro.Categoria,
                        global_id = muro.GlobalId,
                        disciplina = muro.Columna1,
                        sub_disciplina_1 = muro.Columna2,
                        sub_disciplina_2 = muro.Columna3,
                        unidad = muro.Unidad,
                        metrado = muro.Metrado
                    };

                    string json = JsonSerializer.Serialize(data);
                    var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                    var respuesta = Task.Run(async () =>
                        await client.PostAsync("http://localhost:3000/mediciones", contenido)
                    ).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error de conexión", $"No se pudo enviar '{muro.Nombre}': {ex.Message}");
            }
        }

        private string ExportarIFC(Document doc)
        {
            string carpetaTemporal = Path.GetTempPath();
            string nombreArchivo = "modelo-actual.ifc";

            IFCExportOptions opciones = new IFCExportOptions();
            opciones.AddOption("StoreIFCGUID", "true");

            using (Transaction t = new Transaction(doc, "Exportar IFC"))
            {
                t.Start();
                doc.Export(carpetaTemporal, nombreArchivo, opciones);
                t.Commit();
            }

            return Path.Combine(carpetaTemporal, nombreArchivo);
        }

        private void SubirIFC(string rutaCompleta)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);

                    using (var contenido = new MultipartFormDataContent())
                    using (var stream = File.OpenRead(rutaCompleta))
                    {
                        var streamContent = new StreamContent(stream);
                        contenido.Add(streamContent, "archivo", Path.GetFileName(rutaCompleta));

                        var respuesta = Task.Run(async () =>
                            await client.PostAsync("http://localhost:3000/modelo-ifc", contenido)
                        ).GetAwaiter().GetResult();

                        if (!respuesta.IsSuccessStatusCode)
                        {
                            TaskDialog.Show("Error", $"No se pudo subir el modelo IFC: {respuesta.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al exportar/subir el IFC: {ex.Message}");
            }
        }
    }
}