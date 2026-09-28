using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace RevitFamilyCleaner;

internal static class ClassificationStorage
{
    private static readonly Guid SchemaGuid =
        new("9B7D6E90-2D11-4F4A-9E7D-1A1A4D7A2025");

    private const string SchemaName = "MG_ElementClassification";

    public static void SetClassification(
        Element element,
        string disciplina,
        string grupo,
        string subgrupo)
    {
        Schema? schema = Schema.Lookup(SchemaGuid);

        if (schema == null)
        {
            SchemaBuilder builder = new(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetDocumentation(
                "Clasificación de elementos creada por Plugin Clasificador MG.");

            builder.AddSimpleField("Disciplina", typeof(string));
            builder.AddSimpleField("Grupo", typeof(string));
            builder.AddSimpleField("Subgrupo", typeof(string));
            builder.AddSimpleField("ClasificacionCompleta", typeof(string));

            schema = builder.Finish();
        }

        Entity entity = new(schema);

        entity.Set("Disciplina", disciplina);
        entity.Set("Grupo", grupo);
        entity.Set("Subgrupo", subgrupo);
        entity.Set(
            "ClasificacionCompleta",
            $"{disciplina} > {grupo} > {subgrupo}");

        element.SetEntity(entity);
    }

    public static (string Disciplina, string Grupo, string Subgrupo)? GetClassification(
        Element element)
    {
        Schema? schema = Schema.Lookup(SchemaGuid);
        if (schema == null)
            return null;

        Entity entity = element.GetEntity(schema);
        if (!entity.IsValid())
            return null;

        return (
            entity.Get<string>("Disciplina"),
            entity.Get<string>("Grupo"),
            entity.Get<string>("Subgrupo"));
    }

    // ---------------------------------------------------------------
    // Guarda/lee la Unidad (M2, M3, ML, UND) elegida manualmente por el
    // usuario en Sincronizar Muros, para que no se pierda al reabrir.
    // ---------------------------------------------------------------

    private static readonly Guid SchemaGuidUnidad =
        new("1F3C9A20-6E5B-4A11-9C2D-7B5E2F0A9AC1");

    private const string SchemaNameUnidad = "MG_UnidadMetrado";

    public static void SetUnidad(Element element, string unidad)
    {
        Schema? schema = Schema.Lookup(SchemaGuidUnidad);

        if (schema == null)
        {
            SchemaBuilder builder = new(SchemaGuidUnidad);
            builder.SetSchemaName(SchemaNameUnidad);
            builder.SetDocumentation(
                "Unidad de metrado (M2/M3/ML/UND) elegida manualmente en Sincronizar Muros.");

            builder.AddSimpleField("Unidad", typeof(string));

            schema = builder.Finish();
        }

        Entity entity = new(schema);
        entity.Set("Unidad", unidad ?? "");

        element.SetEntity(entity);
    }

    public static string? GetUnidad(Element element)
    {
        Schema? schema = Schema.Lookup(SchemaGuidUnidad);
        if (schema == null)
            return null;

        Entity entity = element.GetEntity(schema);
        if (!entity.IsValid())
            return null;

        return entity.Get<string>("Unidad");
    }
}
