using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitFamilyCleaner
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "Integración BIM";
            application.CreateRibbonTab(tabName);

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Sincronización");

            string rutaEnsamblado = Assembly.GetExecutingAssembly().Location;

            // Botón 1: Sincronizar Muros
            PushButtonData btnSincronizar = new PushButtonData(
                "SincronizarMuros",
                "Sincronizar\nMuros",
                rutaEnsamblado,
                "RevitFamilyCleaner.Command");
            panel.AddItem(btnSincronizar);

            // Botón 2: Enviar Clasificación (envía al backend lo ya guardado en Revit)
            PushButtonData btnEnviarClasificacion = new PushButtonData(
                "EnviarClasificacion",
                "Enviar\nClasificación",
                rutaEnsamblado,
                "RevitFamilyCleaner.EnviarClasificacionCommand");
            panel.AddItem(btnEnviarClasificacion);

            // Botón 3: Clasificar por Grupo (Disciplina > Grupo > Subgrupo, vía Extensible Storage)
            PushButtonData btnClasificarGrupo = new PushButtonData(
                "ClasificarPorGrupo",
                "Clasificar\npor Grupo",
                rutaEnsamblado,
                "RevitFamilyCleaner.ClasificarConGruposCommand");
            panel.AddItem(btnClasificarGrupo);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}