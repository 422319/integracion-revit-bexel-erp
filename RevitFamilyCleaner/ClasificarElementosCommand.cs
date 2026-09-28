using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyCleaner
{
    [Transaction(TransactionMode.Manual)]
    public class ClasificarElementosCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Recolectar todas las líneas de eje (Grids) del proyecto, una sola vez
            List<Grid> grids = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            // 1. Recolectar TODOS los elementos colocados en el modelo, de cualquier categoría
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            List<ElementoItem> listaElementos = new List<ElementoItem>();

            foreach (Element elem in collector)
            {
                // Solo elementos con categoría válida y que acepten los parámetros que vinculamos
                if (elem.Category == null || !elem.Category.AllowsBoundParameters) continue;

                ElementId idTipo = elem.GetTypeId();
                if (idTipo == ElementId.InvalidElementId) continue;

                ElementType tipoElemento = doc.GetElement(idTipo) as ElementType;
                if (tipoElemento == null) continue;

                string familia = tipoElemento.FamilyName;
                string tipo = tipoElemento.Name;

                if (string.IsNullOrEmpty(familia)) continue;

                string discActual = elem.LookupParameter("TITULO_DISCIPLINA")?.AsString() ?? "";
                string subActual = elem.LookupParameter("SUBTITULO_DISCIPLINA")?.AsString() ?? "";
                string detallesActual = elem.LookupParameter("DETALLES_ELEMENTO")?.AsString() ?? "";

                // Ejes: si ya tiene un valor guardado, lo respetamos; si está vacío,
                // lo calculamos automáticamente según la ubicación del elemento.
                string ejesActual = elem.LookupParameter("EJES_ELEMENTO")?.AsString() ?? "";
                if (string.IsNullOrEmpty(ejesActual))
                {
                    XYZ punto = ObtenerPuntoUbicacion(elem);
                    ejesActual = DetectarEjesCercanos(punto, grids);
                }

                listaElementos.Add(new ElementoItem
                {
                    ElementIdValue = elem.Id.Value,
                    Familia = familia,
                    Tipo = tipo,
                    Disciplina = discActual,
                    Subtitulo = subActual,
                    Detalles = detallesActual,
                    Ejes = ejesActual
                });
            }

            if (listaElementos.Count == 0)
            {
                TaskDialog.Show("Clasificar elementos", "No se encontraron elementos en el modelo.");
                return Result.Succeeded;
            }

            // 2. Mostrar la ventana
            ClasificarElementosWindow ventana = new ClasificarElementosWindow(listaElementos);
            bool? resultado = ventana.ShowDialog();

            if (resultado != true)
            {
                return Result.Cancelled;
            }

            // 3. Guardar los valores en los parámetros de cada elemento
            using (Transaction t = new Transaction(doc, "Clasificar elementos por disciplina"))
            {
                t.Start();

                foreach (ElementoItem item in ventana.Elementos)
                {
                    Element elem = doc.GetElement(new ElementId((long)item.ElementIdValue));
                    if (elem == null) continue;

                    Parameter paramTitulo = elem.LookupParameter("TITULO_DISCIPLINA");
                    if (paramTitulo != null && !paramTitulo.IsReadOnly)
                    {
                        paramTitulo.Set(item.Disciplina ?? "");
                    }

                    Parameter paramSubtitulo = elem.LookupParameter("SUBTITULO_DISCIPLINA");
                    if (paramSubtitulo != null && !paramSubtitulo.IsReadOnly)
                    {
                        paramSubtitulo.Set(item.Subtitulo ?? "");
                    }

                    Parameter paramDetalles = elem.LookupParameter("DETALLES_ELEMENTO");
                    if (paramDetalles != null && !paramDetalles.IsReadOnly)
                    {
                        paramDetalles.Set(item.Detalles ?? "");
                    }

                    Parameter paramEjes = elem.LookupParameter("EJES_ELEMENTO");
                    if (paramEjes != null && !paramEjes.IsReadOnly)
                    {
                        paramEjes.Set(item.Ejes ?? "");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Clasificar elementos", $"Se actualizaron {ventana.Elementos.Count} elementos.");

            return Result.Succeeded;
        }

        // Obtiene un punto representativo de la ubicación del elemento
        // (el punto si es una familia puntual, o el punto medio si es una línea, como un muro)
        private XYZ ObtenerPuntoUbicacion(Element elem)
        {
            if (elem.Location is LocationPoint lp)
            {
                return lp.Point;
            }
            else if (elem.Location is LocationCurve lc)
            {
                XYZ inicio = lc.Curve.GetEndPoint(0);
                XYZ fin = lc.Curve.GetEndPoint(1);
                return (inicio + fin) / 2.0;
            }
            else
            {
                BoundingBoxXYZ bb = elem.get_BoundingBox(null);
                if (bb != null)
                {
                    return (bb.Min + bb.Max) / 2.0;
                }
            }
            return null;
        }

        // Busca el eje (o los dos ejes) más cercanos a un punto dado, comparando
        // la distancia perpendicular a cada línea de eje del proyecto
        private string DetectarEjesCercanos(XYZ punto, List<Grid> grids)
        {
            if (punto == null || grids == null || grids.Count == 0) return "";

            var candidatos = new List<(Grid grid, double distancia, XYZ direccion)>();

            foreach (Grid grid in grids)
            {
                Curve curva = grid.Curve;
                if (curva == null) continue;

                XYZ proyeccion = curva.Project(punto)?.XYZPoint;
                if (proyeccion == null) continue;

                double distancia = punto.DistanceTo(proyeccion);
                XYZ direccion = (curva.GetEndPoint(1) - curva.GetEndPoint(0)).Normalize();

                candidatos.Add((grid, distancia, direccion));
            }

            if (candidatos.Count == 0) return "";

            var ordenados = candidatos.OrderBy(c => c.distancia).ToList();
            var primero = ordenados[0];

            // Busca el siguiente eje más cercano que NO sea paralelo al primero
            // (para armar algo tipo "3-B" en vez de dos ejes paralelos redundantes)
            var segundo = ordenados
                .Skip(1)
                .FirstOrDefault(c => primero.direccion.CrossProduct(c.direccion).GetLength() > 0.5);

            if (segundo.grid != null)
            {
                return $"{primero.grid.Name}-{segundo.grid.Name}";
            }

            return primero.grid.Name;
        }
    }
}
