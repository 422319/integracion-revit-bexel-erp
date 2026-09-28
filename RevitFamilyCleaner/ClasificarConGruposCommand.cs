using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitFamilyCleaner;

[Transaction(TransactionMode.Manual)]
public class ClasificarConGruposCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        Document doc = uiDoc.Document;

        var selectedIds = uiDoc.Selection.GetElementIds().ToList();

        if (selectedIds.Count == 0)
        {
            TaskDialog.Show(
                "Clasificador MG",
                "Primero selecciona uno o varios elementos de Revit.");
            return Result.Cancelled;
        }

        var dialog = new ClasificacionGrupoWindow();
        bool? result = dialog.ShowDialog();

        if (result != true)
            return Result.Cancelled;

        string disciplina = dialog.Disciplina;
        string grupo = dialog.Grupo;
        string subgrupo = dialog.Subgrupo;

        int updated = 0;
        int skipped = 0;

        using Transaction tx = new(doc, "Clasificar elementos - MG");
        tx.Start();

        foreach (ElementId id in selectedIds)
        {
            Element? element = doc.GetElement(id);
            if (element == null)
            {
                skipped++;
                continue;
            }

            try
            {
                ClassificationStorage.SetClassification(
                    element,
                    disciplina,
                    grupo,
                    subgrupo);

                // También escribe un valor legible en Comentarios, si el parámetro existe.
                Parameter? comments = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comments != null && !comments.IsReadOnly)
                {
                    comments.Set(
                        $"MG | {disciplina} | {grupo} | {subgrupo}");
                }

                updated++;
            }
            catch
            {
                skipped++;
            }
        }

        tx.Commit();

        TaskDialog.Show(
            "Clasificador MG",
            $"Clasificación aplicada.\n\n" +
            $"Elementos seleccionados: {selectedIds.Count}\n" +
            $"Actualizados: {updated}\n" +
            $"No modificados: {skipped}\n\n" +
            $"Clasificación:\n{disciplina} > {grupo} > {subgrupo}");

        return Result.Succeeded;
    }
}
