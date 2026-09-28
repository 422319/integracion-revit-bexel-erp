using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RevitFamilyCleaner
{
    // Convierte el nombre de una categoría de Revit (Muros, Rejillas de aire, Puertas, etc.)
    // en un color consistente. Cualquier categoría nueva que aparezca recibe automáticamente
    // un color de la paleta, siempre el mismo para esa categoría.
    public class CategoriaColorConverter : IValueConverter
    {
        private static readonly Color[] Paleta = new Color[]
        {
            Color.FromRgb(0xD6, 0xE9, 0xF8), // azul claro
            Color.FromRgb(0xE3, 0xF3, 0xDD), // verde claro
            Color.FromRgb(0xFD, 0xEC, 0xD2), // naranja claro
            Color.FromRgb(0xF6, 0xDD, 0xE8), // rosado claro
            Color.FromRgb(0xE6, 0xE0, 0xF7), // lila claro
            Color.FromRgb(0xFC, 0xE3, 0xDA), // durazno claro
            Color.FromRgb(0xD0, 0xF3, 0xF0), // turquesa claro
            Color.FromRgb(0xF0, 0xEA, 0xD6), // beige claro
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string categoria = value as string;

            if (string.IsNullOrWhiteSpace(categoria))
            {
                return Brushes.White;
            }

            int indice = Math.Abs(categoria.GetHashCode()) % Paleta.Length;
            return new SolidColorBrush(Paleta[indice]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
