import { useState, useEffect } from 'react';

function App() {
  const [mediciones, setMediciones] = useState([]);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    fetch('http://localhost:3000/mediciones')
      .then((respuesta) => respuesta.json())
      .then((datos) => {
        setMediciones(datos);
        setCargando(false);
      })
      .catch((error) => {
        console.error('Error al cargar mediciones:', error);
        setCargando(false);
      });
  }, []);

  if (cargando) {
    return <p>Cargando mediciones...</p>;
  }

  return (
    <div style={{ padding: '2rem' }}>
      <h1>Mediciones del Modelo BIM</h1>
      <table border="1" cellPadding="8" style={{ borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th>ID</th>
            <th>Elemento</th>
            <th>Categoría</th>
            <th>Volumen</th>
            <th>Área</th>
            <th>Fecha</th>
          </tr>
        </thead>
        <tbody>
          {mediciones.map((m) => (
            <tr key={m.id}>
              <td>{m.id}</td>
              <td>{m.nombre_elemento}</td>
              <td>{m.categoria}</td>
              <td>{m.volumen}</td>
              <td>{m.area}</td>
              <td>{m.fecha_sincronizacion}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default App;