using System.Windows;
using System.Windows.Controls;

namespace RevitFamilyCleaner;

public partial class ClasificacionGrupoWindow : Window
{
    private readonly Dictionary<string, List<string>> grupos =
        new()
        {
            ["ESTRUCTURA"] = new()
            {
                "CIMENTACIONES",
                "SOBRECIMIENTOS",
                "COLUMNAS",
                "VIGAS",
                "LOSAS",
                "MUROS ESTRUCTURALES",
                "OTROS"
            },
            ["ARQUITECTURA"] = new()
            {
                "MUROS",
                "PUERTAS",
                "VENTANAS",
                "PISOS",
                "TECHOS",
                "OTROS"
            },
            ["ELÉCTRICO"] = new()
            {
                "ALUMBRADO",
                "TOMACORRIENTES",
                "TABLEROS",
                "CANALIZACIONES",
                "OTROS"
            },
            ["SANITARIAS"] = new()
            {
                "AGUA",
                "DESAGÜE",
                "VENTILACIÓN",
                "OTROS"
            },
            ["MECÁNICA"] = new()
            {
                "HVAC",
                "EQUIPOS",
                "DUCTOS",
                "OTROS"
            },
            ["OTROS"] = new()
            {
                "GENERAL"
            }
        };

    public string Disciplina =>
        DisciplinaCombo.SelectedItem?.ToString() ?? "";

    public string Grupo =>
        GrupoCombo.SelectedItem?.ToString() ?? "";

    public string Subgrupo =>
        SubgrupoCombo.SelectedItem?.ToString() ?? "";

    public ClasificacionGrupoWindow()
    {
        InitializeComponent();

        foreach (string disciplina in grupos.Keys)
            DisciplinaCombo.Items.Add(disciplina);

        DisciplinaCombo.SelectedIndex = 0;
        UpdatePreview();
    }

    private void DisciplinaCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        GrupoCombo.Items.Clear();

        if (DisciplinaCombo.SelectedItem is string disciplina &&
            grupos.TryGetValue(disciplina, out List<string>? lista))
        {
            foreach (string grupo in lista)
                GrupoCombo.Items.Add(grupo);

            if (GrupoCombo.Items.Count > 0)
                GrupoCombo.SelectedIndex = 0;
        }

        SubgrupoCombo.Items.Clear();
        SubgrupoCombo.Items.Add("GENERAL");
        SubgrupoCombo.Items.Add("PRINCIPAL");
        SubgrupoCombo.Items.Add("SECUNDARIO");
        SubgrupoCombo.SelectedIndex = 0;

        UpdatePreview();
    }

    private void Aplicar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Disciplina) ||
            string.IsNullOrWhiteSpace(Grupo) ||
            string.IsNullOrWhiteSpace(Subgrupo))
        {
            MessageBox.Show(
                "Selecciona Disciplina, Grupo y Subgrupo.",
                "Clasificador MG",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdatePreview()
    {
        PreviewText.Text =
            $"Vista previa: {Disciplina} > {Grupo} > {Subgrupo}";
    }
}
