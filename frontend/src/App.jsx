import { useState, useEffect, useRef } from 'react';
import { IfcViewerAPI } from 'web-ifc-viewer';
import ArbolMediciones from './ArbolMediciones';
import './App.css';

function App() {
  const [mediciones, setMediciones] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);

  const contenedorRef = useRef(null);
  const viewerRef = useRef(null);
  const mapaGlobalIdRef = useRef(new Map());
  const [cargandoModelo, setCargandoModelo] = useState(false);

  const cargarModeloIFC = async () => {
    if (!viewerRef.current) return;
    try {
      setCargandoModelo(true);
      await viewerRef.current.IFC.loadIfcUrl('http://192.168.1.71:3000/modelo-ifc');
      await construirMapaGlobalId();
    } catch (err) {
      console.log('Todavía no hay un modelo IFC subido, o hubo un error al cargarlo:', err);
    } finally {
      setCargandoModelo(false);
    }
  };

  // Recorre el modelo IFC una sola vez (al cargarlo) y arma un mapa
  // GlobalId -> expressID, para poder resaltar elementos al instante
  const construirMapaGlobalId = async () => {
    if (!viewerRef.current) return;

    try {
      const modeloID = 0;
      const propertyManager = viewerRef.current.IFC.loader.ifcManager;

      // getSpatialStructure trae todo el árbol del modelo (proyecto > sitio > edificio > piso > elementos)
      // con sus propiedades incluidas, sin necesidad de saber de antemano qué tipos de elemento buscar
      const estructura = await propertyManager.getSpatialStructure(modeloID, true);

      const mapa = new Map();

      const recorrerNodo = (nodo) => {
        if (nodo?.GlobalId?.value && nodo?.expressID != null) {
          mapa.set(nodo.GlobalId.value, nodo.expressID);
        }
        if (Array.isArray(nodo?.children)) {
          nodo.children.forEach(recorrerNodo);
        }
      };

      recorrerNodo(estructura);

      mapaGlobalIdRef.current = mapa;
    } catch (err) {
      console.error('Error al indexar el modelo IFC:', err);
    }
  };

  const cargarMediciones = () => {
    setCargando(true);
    setError(null);
    fetch('http://192.168.1.71:3000/mediciones')
      .then((respuesta) => respuesta.json())
      .then((datos) => {
        setMediciones(datos);
        setCargando(false);
      })
      .catch((err) => {
        console.error('Error al cargar mediciones:', err);
        setError('No se pudo conectar con el servidor backend.');
        setCargando(false);
      });

    // También recarga el modelo 3D más reciente
    cargarModeloIFC();
  };

  useEffect(() => {
    cargarMediciones();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Inicializa el visor 3D una sola vez, cuando el contenedor ya existe
  useEffect(() => {
    if (contenedorRef.current && !viewerRef.current) {
      const viewer = new IfcViewerAPI({
        container: contenedorRef.current,
        backgroundColor: { r: 0.05, g: 0.05, b: 0.08 },
      });
      viewer.axes.setAxes();
      viewer.grid.setGrid();
      viewer.IFC.setWasmPath('./');
      viewerRef.current = viewer;

      // Ya que el visor recién quedó listo, intenta cargar el modelo actual
      cargarModeloIFC();

      // Fuerza al visor a reajustar su tamaño cada vez que el panel cambia de tamaño
      // (soluciona que el modelo aparezca recortado o desplazado)
      const observador = new ResizeObserver(() => {
        window.dispatchEvent(new Event('resize'));
      });
      observador.observe(contenedorRef.current);

      // También lo dispara una vez apenas se monta, por si el layout ya estaba listo
      setTimeout(() => window.dispatchEvent(new Event('resize')), 200);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const manejarSubidaIFC = async (evento) => {
    const archivo = evento.target.files[0];
    if (!archivo || !viewerRef.current) return;

    setCargandoModelo(true);
    try {
      await viewerRef.current.IFC.loadIfc(archivo, true);
      await construirMapaGlobalId();
    } catch (err) {
      console.error('Error al cargar el modelo IFC:', err);
      alert('No se pudo cargar el archivo IFC. Revisa que sea un archivo válido.');
    } finally {
      setCargandoModelo(false);
    }
  };

  // Resalta y enfoca en el visor 3D el elemento cuyo GlobalId coincida
  const mostrarEnVisor3D = (globalId) => {
    if (!globalId || !viewerRef.current) return;

    const expressId = mapaGlobalIdRef.current.get(globalId);

    if (expressId == null) {
      console.log('No se encontró en el visor 3D el elemento con GlobalId:', globalId);
      return;
    }

    viewerRef.current.IFC.selector.pickIfcItemsByID(0, [expressId], true);
  };

  const totalMetrado = mediciones.reduce((sum, m) => sum + parseFloat(m.metrado || 0), 0);

  return (
    <div className="contenedor">
      <header className="cabecera">
        <h1>🏗️ Mediciones del Modelo BIM</h1>
        <button onClick={cargarMediciones} className="boton-recargar">
          ↻ Recargar
        </button>
      </header>

      {error && <div className="mensaje-error">{error}</div>}

      <div className="layout-dos-filas">
        {/* Fila superior: visor 3D IFC */}
        <div className="fila-visor">
          <div className="visor-cabecera">
            <label htmlFor="subir-ifc" className="boton-subir-ifc">
              📂 Subir plano IFC manualmente
            </label>
            <input
              id="subir-ifc"
              type="file"
              accept=".ifc"
              onChange={manejarSubidaIFC}
              style={{ display: 'none' }}
            />
            {cargandoModelo && <span className="cargando-modelo">Cargando modelo...</span>}
          </div>
          <div ref={contenedorRef} className="visor-3d"></div>
        </div>

        {/* Fila inferior: estadísticas y tabla */}
        <div className="fila-mediciones">
          {!error && (
            <>
              <div className="tarjetas-resumen">
                <div className="tarjeta">
                  <span className="tarjeta-valor">{mediciones.length}</span>
                  <span className="tarjeta-etiqueta">Elementos</span>
                </div>
                <div className="tarjeta">
                  <span className="tarjeta-valor">{totalMetrado.toFixed(2)}</span>
                  <span className="tarjeta-etiqueta">Metrado total</span>
                </div>
              </div>

              {cargando ? (
                <p className="cargando">Cargando mediciones...</p>
              ) : (
                <ArbolMediciones mediciones={mediciones} onSeleccionar={mostrarEnVisor3D} />
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default App;
