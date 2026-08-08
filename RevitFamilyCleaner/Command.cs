using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

            // Recolectar muros del modelo como ejemplo inicial--
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            int enviados = 0;

            foreach (Element elem in collector)
            {
                Wall wall = elem as Wall;
                if (wall == null) continue;

                double volumen = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
                double area = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0;

                EnviarMedicion(wall.Name, "Muros", volumen, area);
                enviados++;
            }

            TaskDialog.Show("Sincronización", $"Se enviaron {enviados} muros al backend.");

            return Result.Succeeded;
        }

        private void EnviarMedicion(string nombre, string categoria, double volumen, double area)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var datos = new
                    {
                        nombre_elemento = nombre,
                        categoria = categoria,
                        volumen = volumen,
                        area = area
                    };

                    string json = JsonSerializer.Serialize(datos);
                    var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                    // Ejecutar en un hilo separado para no bloquear la interfaz de Revit
                    var respuesta = Task.Run(async () =>
                        await client.PostAsync("http://localhost:3000/mediciones", contenido)
                    ).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error de conexión", $"No se pudo enviar '{nombre}': {ex.Message}");
            }
        }
    }
}