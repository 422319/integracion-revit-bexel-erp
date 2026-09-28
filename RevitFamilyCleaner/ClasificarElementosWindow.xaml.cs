using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RevitFamilyCleaner
{
    public partial class ClasificarElementosWindow : Window
    {
        public List<ElementoItem> Elementos { get; private set; }
        public bool Confirmado { get; private set; } = false;

        // Sigla que corresponde a cada disciplina, para armar el Subtítulo
        private static readonly Dictionary<string, string> Siglas = new Dictionary<string, string>
        {
            { "Arquitectura", "ARQ" },
            { "Estructura", "EST" },
            { "Instalaciones Eléctricas", "IE" },
            { "Instalaciones Sanitarias", "IS" },
            { "Otros", "OTR" },
        };

        public ClasificarElementosWindow(List<ElementoItem> elementos)
        {
            InitializeComponent();
            Elementos = elementos;
            GridElementos.ItemsSource = Elementos;
        }

        private void Autonumerar_Click(object sender, RoutedEventArgs e)
        {
            // Agrupa por disciplina y asigna números consecutivos SIN repetir, por grupo
            var grupos = Elementos
                .Where(el => !string.IsNullOrEmpty(el.Disciplina))
                .GroupBy(el => el.Disciplina);

            foreach (var grupo in grupos)
            {
                string sigla = Siglas.ContainsKey(grupo.Key) ? Siglas[grupo.Key] : "OTR";
                int contador = 1;
                foreach (var el in grupo)
                {
                    el.Subtitulo = $"{sigla}-{contador:00}";
                    contador++;
                }
            }

            GridElementos.Items.Refresh();
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = true;
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = false;
            DialogResult = false;
            Close();
        }
    }
}