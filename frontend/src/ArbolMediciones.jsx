import { useState, useMemo } from 'react';

// Agrupa una lista plana de mediciones en el árbol Disciplina -> Sub-Disciplina 1 -> Sub-Disciplina 2 -> Elementos
function construirArbol(mediciones) {
  const raiz = {};

  mediciones.forEach((m) => {
    const disciplina = m.disciplina?.trim() || '(sin disciplina)';
    const sub1 = m.sub_disciplina_1?.trim() || '(sin asignar)';
    const sub2 = m.sub_disciplina_2?.trim() || '(sin asignar)';

    raiz[disciplina] ??= {};
    raiz[disciplina][sub1] ??= {};
    raiz[disciplina][sub1][sub2] ??= [];
    raiz[disciplina][sub1][sub2].push(m);
  });

  const nodos = [];
  let numDisciplina = 0;

  for (const disciplina in raiz) {
    numDisciplina++;
    const itemDisciplina = `${numDisciplina}`;
    const hijosDisciplina = [];
    let numSub1 = 0;

    for (const sub1 in raiz[disciplina]) {
      numSub1++;
      const itemSub1 = `${itemDisciplina}.${numSub1}`;
      const hijosSub1 = [];
      let numSub2 = 0;

      for (const sub2 in raiz[disciplina][sub1]) {
        numSub2++;
        const itemSub2 = `${itemSub1}.${numSub2}`;
        const elementos = raiz[disciplina][sub1][sub2].map((m, i) => ({
          item: `${itemSub2}.${i + 1}`,
          nombre: m.nombre_elemento,
          unidad: m.unidad,
          metrado: m.metrado,
          id: m.id,
          globalId: m.global_id,
        }));

        hijosSub1.push({ item: itemSub2, nombre: sub2, hijos: elementos });
      }

      hijosDisciplina.push({ item: itemSub1, nombre: sub1, hijos: hijosSub1 });
    }

    nodos.push({ item: itemDisciplina, nombre: disciplina, hijos: hijosDisciplina });
  }

  return nodos;
}

function NodoArbol({ nodo, nivel, onSeleccionar }) {
  const [abierto, setAbierto] = useState(true);
  const esHoja = !nodo.hijos;

  const manejarClic = () => {
    if (esHoja) {
      onSeleccionar?.(nodo.globalId);
    } else {
      setAbierto(!abierto);
    }
  };

  return (
    <div>
      <div
        className="arbol-fila"
        style={{ paddingLeft: `${nivel * 22}px` }}
        onClick={manejarClic}
      >
        <span className="arbol-flecha">
          {!esHoja ? (abierto ? '▾' : '▸') : ''}
        </span>
        <input type="checkbox" className="arbol-check" onClick={(e) => e.stopPropagation()} readOnly />
        <span className="arbol-item">{nodo.item}</span>
        <span className={esHoja ? 'arbol-nombre-hoja' : 'arbol-nombre-grupo'}>
          {nodo.nombre}
        </span>
        {esHoja && (
          <>
            <span className="arbol-unidad">{nodo.unidad}</span>
            <span className="arbol-metrado">{parseFloat(nodo.metrado || 0).toFixed(2)}</span>
          </>
        )}
      </div>

      {!esHoja && abierto && (
        <div>
          {nodo.hijos.map((hijo) => (
            <NodoArbol key={hijo.item} nodo={hijo} nivel={nivel + 1} onSeleccionar={onSeleccionar} />
          ))}
        </div>
      )}
    </div>
  );
}

export default function ArbolMediciones({ mediciones, onSeleccionar }) {
  const arbol = useMemo(() => construirArbol(mediciones), [mediciones]);

  return (
    <div className="arbol-contenedor">
      <div className="arbol-encabezado">
        <span className="arbol-encabezado-numero">2</span>
        <span className="arbol-encabezado-check">Enviar</span>
        <span className="arbol-encabezado-item">Item</span>
        <span className="arbol-encabezado-nombre">Descripción</span>
        <span className="arbol-encabezado-unidad">Uni.</span>
        <span className="arbol-encabezado-metrado">Metrado</span>
      </div>

      <div className="arbol-cuerpo">
        {arbol.length === 0 ? (
          <p className="arbol-vacio">No hay elementos sincronizados todavía.</p>
        ) : (
          arbol.map((nodo) => <NodoArbol key={nodo.item} nodo={nodo} nivel={0} onSeleccionar={onSeleccionar} />)
        )}
      </div>
    </div>
  );
}
