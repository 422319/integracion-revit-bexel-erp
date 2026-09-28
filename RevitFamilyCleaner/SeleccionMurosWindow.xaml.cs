using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace RevitFamilyCleaner
{
    // Representa cada fila de la tabla
    public class MuroItem : INotifyPropertyChanged
    {
        private bool seleccionado;

        public bool Seleccionado
        {
            get => seleccionado;
            set
            {
                seleccionado = value;
                OnPropertyChanged(nameof(Seleccionado));
            }
        }

        public long ElementIdValue { get; set; }

        public string Nombre { get; set; }
        public string Categoria { get; set; }
        public string GlobalId { get; set; }
        public double Area { get; set; }
        public double Volumen { get; set; }

        private string unidad;
        public string Unidad
        {
            get => unidad;
            set
            {
                unidad = value;
                OnPropertyChanged(nameof(Unidad));
                OnPropertyChanged(nameof(Metrado));
            }
        }

        // El metrado se calcula según la unidad elegida, no queda fijo
        public double Metrado
        {
            get
            {
                if (Unidad == "M3") return Volumen;
                if (Unidad == "M2") return Area;
                if (Unidad == "ML") return 0; // reservado por si luego se calcula longitud
                return 1; // UND
            }
        }

        private string columna1;
        public string Columna1
        {
            get => columna1;
            set { columna1 = value; OnPropertyChanged(nameof(Columna1)); }
        }

        private string columna2;
        public string Columna2
        {
            get => columna2;
            set { columna2 = value; OnPropertyChanged(nameof(Columna2)); }
        }

        private string columna3;
        public string Columna3
        {
            get => columna3;
            set { columna3 = value; OnPropertyChanged(nameof(Columna3)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string nombre) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }

    // Un nodo del árbol de informe (Disciplina / Sub-Disciplina 1 / Sub-Disciplina 2 / Elemento)
    public class TreeNodo : INotifyPropertyChanged
    {
        public string Item { get; set; } = "";
        public string Nombre { get; set; }
        public string UnidadTexto { get; set; } = "";
        public string MetradoTexto { get; set; } = "";
        public MuroItem MuroReferencia { get; set; } = null;
        public TreeNodo Padre { get; set; } = null;
        public ObservableCollection<TreeNodo> Hijos { get; set; } = new ObservableCollection<TreeNodo>();

        public bool? Seleccionado
        {
            get
            {
                if (MuroReferencia != null)
                {
                    return MuroReferencia.Seleccionado;
                }

                if (Hijos.Count == 0) return false;

                bool todosMarcados = Hijos.All(h => h.Seleccionado == true);
                bool ningunoMarcado = Hijos.All(h => h.Seleccionado == false);

                if (todosMarcados) return true;
                if (ningunoMarcado) return false;
                return null; // estado mixto (indeterminado)
            }
            set
            {
                if (MuroReferencia != null)
                {
                    // Nodo hoja (un muro): marca/desmarca el muro y avisa hacia arriba
                    MuroReferencia.Seleccionado = value == true;
                    OnPropertyChanged(nameof(Seleccionado));
                    Padre?.NotificarCambioHaciaArriba();
                }
                else
                {
                    // Nodo de grupo: propaga el valor a todos los hijos (cascada hacia abajo)
                    foreach (TreeNodo hijo in Hijos)
                    {
                        hijo.Seleccionado = value;
                    }

                    OnPropertyChanged(nameof(Seleccionado));
                }
            }
        }

        // Recalcula y notifica el propio estado, y sigue avisando hacia arriba
        public void NotificarCambioHaciaArriba()
        {
            OnPropertyChanged(nameof(Seleccionado));
            Padre?.NotificarCambioHaciaArriba();
        }

        // Busca, dentro de este nodo o sus descendientes, el nodo hoja que representa a "muro"
        public TreeNodo BuscarNodoDeMuro(MuroItem muro)
        {
            if (MuroReferencia == muro) return this;

            foreach (TreeNodo hijo in Hijos)
            {
                TreeNodo encontrado = hijo.BuscarNodoDeMuro(muro);
                if (encontrado != null) return encontrado;
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string nombre) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }

    public partial class SeleccionMurosWindow : Window
    {
        public ObservableCollection<MuroItem> Muros { get; set; }
        public List<MuroItem> Seleccionados { get; private set; } = new List<MuroItem>();
        public ObservableCollection<TreeNodo> Arbol { get; set; } = new ObservableCollection<TreeNodo>();
        public bool SoloGuardar { get; private set; } = false;

        // Valores ya escritos, para autocompletar Sub-Disciplina 1 y 2 en otras filas
        public ObservableCollection<string> SugerenciasSub1 { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SugerenciasSub2 { get; } = new ObservableCollection<string>();

        private readonly Autodesk.Revit.UI.UIDocument uidoc;
        private readonly List<Autodesk.Revit.DB.ElementId> idsConOverrideAplicado = new List<Autodesk.Revit.DB.ElementId>();

        public SeleccionMurosWindow(List<MuroItem> muros, Autodesk.Revit.UI.UIDocument uidoc)
        {
            InitializeComponent();
            this.uidoc = uidoc;
            Muros = new ObservableCollection<MuroItem>(muros);
            GridMuros.ItemsSource = Muros;
            ArbolInforme.ItemsSource = Arbol;

            this.SourceInitialized += SeleccionMurosWindow_SourceInitialized;
            this.Closed += (s, e) => RestaurarVistaNormal();

            foreach (MuroItem muro in Muros)
            {
                muro.PropertyChanged += Muro_PropertyChanged;

                if (!string.IsNullOrWhiteSpace(muro.Columna2) && !SugerenciasSub1.Contains(muro.Columna2))
                    SugerenciasSub1.Add(muro.Columna2);

                if (!string.IsNullOrWhiteSpace(muro.Columna3) && !SugerenciasSub2.Contains(muro.Columna3))
                    SugerenciasSub2.Add(muro.Columna3);
            }

            ActualizarArbol();
        }

        private void Muro_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MuroItem.Columna1) ||
                e.PropertyName == nameof(MuroItem.Columna2) ||
                e.PropertyName == nameof(MuroItem.Columna3) ||
                e.PropertyName == nameof(MuroItem.Unidad))
            {
                ActualizarArbol();
            }
            else if (e.PropertyName == nameof(MuroItem.Seleccionado))
            {
                // El check se cambió desde la tabla de arriba: avisar al árbol
                MuroItem muro = sender as MuroItem;
                if (muro == null) return;

                foreach (TreeNodo raiz in Arbol)
                {
                    TreeNodo nodoHoja = raiz.BuscarNodoDeMuro(muro);
                    if (nodoHoja != null)
                    {
                        nodoHoja.NotificarCambioHaciaArriba();
                        break;
                    }
                }
            }
        }

        private void ActualizarArbol()
        {
            Arbol.Clear();

            var conDisciplina = Muros.Where(m => !string.IsNullOrWhiteSpace(m.Columna1));

            var porDisciplina = conDisciplina.GroupBy(m => m.Columna1);

            int numDisciplina = 0;
            foreach (var grupoDisciplina in porDisciplina)
            {
                numDisciplina++;
                string itemDisciplina = numDisciplina.ToString();
                TreeNodo nodoDisciplina = new TreeNodo { Item = itemDisciplina, Nombre = grupoDisciplina.Key };

                var porSub1 = grupoDisciplina.GroupBy(m => string.IsNullOrWhiteSpace(m.Columna2) ? "(sin asignar)" : m.Columna2);

                int numSub1 = 0;
                foreach (var grupoSub1 in porSub1)
                {
                    numSub1++;
                    string itemSub1 = $"{itemDisciplina}.{numSub1}";
                    TreeNodo nodoSub1 = new TreeNodo { Item = itemSub1, Nombre = grupoSub1.Key, Padre = nodoDisciplina };

                    var porSub2 = grupoSub1.GroupBy(m => string.IsNullOrWhiteSpace(m.Columna3) ? "(sin asignar)" : m.Columna3);

                    int numSub2 = 0;
                    foreach (var grupoSub2 in porSub2)
                    {
                        numSub2++;
                        string itemSub2 = $"{itemSub1}.{numSub2}";
                        TreeNodo nodoSub2 = new TreeNodo { Item = itemSub2, Nombre = grupoSub2.Key, Padre = nodoSub1 };

                        int numElemento = 0;
                        foreach (MuroItem muro in grupoSub2)
                        {
                            numElemento++;
                            nodoSub2.Hijos.Add(new TreeNodo
                            {
                                Item = $"{itemSub2}.{numElemento}",
                                Nombre = muro.Nombre,
                                UnidadTexto = muro.Unidad,
                                MetradoTexto = muro.Metrado.ToString("0.00"),
                                MuroReferencia = muro,
                                Padre = nodoSub2
                            });
                        }

                        nodoSub1.Hijos.Add(nodoSub2);
                    }

                    nodoDisciplina.Hijos.Add(nodoSub1);
                }

                Arbol.Add(nodoDisciplina);
            }
        }

        private const int WM_NCLBUTTONDBLCLK = 0x00A3;
        private bool ventanaReducida = false;
        private double alturaOriginal;
        private double anchoOriginal;
        private double minAlturaOriginal;
        private double leftOriginal;
        private double topOriginal;

        private void SeleccionMurosWindow_SourceInitialized(object sender, System.EventArgs e)
        {
            alturaOriginal = this.Height;
            anchoOriginal = this.Width;
            minAlturaOriginal = this.MinHeight;
            leftOriginal = this.Left;
            topOriginal = this.Top;

            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source?.AddHook(InterceptarDobleClicTitulo);
        }

        private System.IntPtr InterceptarDobleClicTitulo(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                handled = true; // evita que Windows maximice la ventana por defecto

                if (!ventanaReducida)
                {
                    alturaOriginal = this.Height;
                    anchoOriginal = this.Width;
                    minAlturaOriginal = this.MinHeight;
                    leftOriginal = this.Left;
                    topOriginal = this.Top;

                    double anchoReducido = 500;
                    double altoReducido = 45;

                    this.MinHeight = 45; // deja bajar el mínimo, si no la ventana no se reduce de verdad
                    this.Width = anchoReducido;
                    this.Height = altoReducido;

                    // Centrar la ventana reducida en la pantalla, donde suele quedar visible el modelo
                    this.Left = (SystemParameters.PrimaryScreenWidth - anchoReducido) / 2;
                    this.Top = (SystemParameters.PrimaryScreenHeight - altoReducido) / 2;

                    this.Opacity = 0.35; // deja ver el modelo "a través" de la ventana

                    ventanaReducida = true;
                }
                else
                {
                    this.MinHeight = minAlturaOriginal;
                    this.Height = alturaOriginal;
                    this.Width = anchoOriginal;
                    this.Left = leftOriginal;
                    this.Top = topOriginal;
                    this.Opacity = 1.0;

                    ventanaReducida = false;
                }
            }

            return System.IntPtr.Zero;
        }

        private void ComboUnidad_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo &&
                combo.DataContext is MuroItem muro &&
                combo.SelectedItem is string nuevaUnidad &&
                muro.Unidad != nuevaUnidad)
            {
                muro.Unidad = nuevaUnidad; // dispara OnPropertyChanged(Unidad) y (Metrado)
                ActualizarArbol(); // fuerza también la actualización del árbol de abajo
            }
        }

        private void SeleccionarEnModelo(MuroItem muro)
        {
            if (uidoc == null || muro == null) return;

            try
            {
                Autodesk.Revit.DB.ElementId id = new Autodesk.Revit.DB.ElementId((long)muro.ElementIdValue);
                uidoc.Selection.SetElementIds(new List<Autodesk.Revit.DB.ElementId> { id });
                uidoc.ShowElements(id);

                AplicarTransparenciaExceptoUno(id);
            }
            catch
            {
                // Si el elemento ya no existe o no se puede seleccionar, no interrumpe la ventana
            }
        }

        // Vuelve transparentes todos los elementos de la vista activa, excepto el que se acaba de seleccionar
        private void AplicarTransparenciaExceptoUno(Autodesk.Revit.DB.ElementId idResaltado)
        {
            Autodesk.Revit.DB.Document doc = uidoc.Document;
            Autodesk.Revit.DB.View vistaActiva = doc.ActiveView;

            Autodesk.Revit.DB.OverrideGraphicSettings transparente = new Autodesk.Revit.DB.OverrideGraphicSettings();
            transparente.SetSurfaceTransparency(85);

            Autodesk.Revit.DB.OverrideGraphicSettings normal = new Autodesk.Revit.DB.OverrideGraphicSettings();

            using (Autodesk.Revit.DB.Transaction t = new Autodesk.Revit.DB.Transaction(doc, "Resaltar elemento seleccionado"))
            {
                t.Start();

                foreach (Autodesk.Revit.DB.ElementId idElemento in idsConOverrideAplicado)
                {
                    try { vistaActiva.SetElementOverrides(idElemento, normal); } catch { }
                }
                idsConOverrideAplicado.Clear();

                Autodesk.Revit.DB.FilteredElementCollector collector =
                    new Autodesk.Revit.DB.FilteredElementCollector(doc, vistaActiva.Id)
                        .WhereElementIsNotElementType();

                foreach (Autodesk.Revit.DB.Element elem in collector)
                {
                    if (elem.Id == idResaltado) continue;
                    if (elem.Category == null || !elem.Category.AllowsBoundParameters) continue;

                    try
                    {
                        vistaActiva.SetElementOverrides(elem.Id, transparente);
                        idsConOverrideAplicado.Add(elem.Id);
                    }
                    catch { }
                }

                t.Commit();
            }
        }

        // Quita todas las transparencias aplicadas, dejando la vista como estaba
        private void RestaurarVistaNormal()
        {
            if (uidoc == null || idsConOverrideAplicado.Count == 0) return;

            Autodesk.Revit.DB.Document doc = uidoc.Document;
            Autodesk.Revit.DB.View vistaActiva = doc.ActiveView;
            Autodesk.Revit.DB.OverrideGraphicSettings normal = new Autodesk.Revit.DB.OverrideGraphicSettings();

            using (Autodesk.Revit.DB.Transaction t = new Autodesk.Revit.DB.Transaction(doc, "Restaurar vista normal"))
            {
                t.Start();

                foreach (Autodesk.Revit.DB.ElementId idElemento in idsConOverrideAplicado)
                {
                    try { vistaActiva.SetElementOverrides(idElemento, normal); } catch { }
                }

                t.Commit();
            }

            idsConOverrideAplicado.Clear();
        }

        private void ArbolInforme_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeNodo nodo && nodo.MuroReferencia != null)
            {
                GridMuros.SelectedItem = nodo.MuroReferencia;
                GridMuros.ScrollIntoView(nodo.MuroReferencia);
                SeleccionarEnModelo(nodo.MuroReferencia);
            }
        }

        private void GridMuros_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (GridMuros.SelectedItem is MuroItem muro)
            {
                SeleccionarEnModelo(muro);
            }
        }

        private void ComboSub1_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo &&
                combo.DataContext is MuroItem muro &&
                !string.IsNullOrWhiteSpace(muro.Columna2) &&
                !SugerenciasSub1.Contains(muro.Columna2))
            {
                SugerenciasSub1.Add(muro.Columna2);
            }
        }

        private void ComboSub2_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo &&
                combo.DataContext is MuroItem muro &&
                !string.IsNullOrWhiteSpace(muro.Columna3) &&
                !SugerenciasSub2.Contains(muro.Columna3))
            {
                SugerenciasSub2.Add(muro.Columna3);
            }
        }

        private void MarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var muro in Muros)
                muro.Seleccionado = true;
        }

        private void DesmarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var muro in Muros)
                muro.Seleccionado = false;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            SoloGuardar = true;
            Seleccionados = new List<MuroItem>(Muros);
            DialogResult = true;
            Close();
        }

        private void Enviar_Click(object sender, RoutedEventArgs e)
        {
            Seleccionados = new List<MuroItem>();
            foreach (var muro in Muros)
            {
                if (muro.Seleccionado)
                    Seleccionados.Add(muro);
            }

            if (Seleccionados.Count == 0)
            {
                MessageBox.Show("No marcaste ningún muro para enviar.", "Aviso");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
