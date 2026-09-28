using System.ComponentModel;

namespace RevitFamilyCleaner
{
    public class ElementoItem : INotifyPropertyChanged
    {
        public long ElementIdValue { get; set; }

        public string Familia { get; set; }
        public string Tipo { get; set; }

        private string _disciplina;
        public string Disciplina
        {
            get => _disciplina;
            set { _disciplina = value; OnPropertyChanged(nameof(Disciplina)); }
        }

        private string _subtitulo;
        public string Subtitulo
        {
            get => _subtitulo;
            set { _subtitulo = value; OnPropertyChanged(nameof(Subtitulo)); }
        }

        private string _detalles;
        public string Detalles
        {
            get => _detalles;
            set { _detalles = value; OnPropertyChanged(nameof(Detalles)); }
        }

        private string _ejes;
        public string Ejes
        {
            get => _ejes;
            set { _ejes = value; OnPropertyChanged(nameof(Ejes)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string nombre) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}