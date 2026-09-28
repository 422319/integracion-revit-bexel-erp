require('dotenv').config();
const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');
const multer = require('multer');
const path = require('path');

const app = express();
app.use(cors());
app.use(express.json());

const pool = new Pool({
    user: 'postgres',
    host: 'localhost',
    database: 'bim_integracion',
    password: process.env.DB_PASSWORD,
    port: 5432,
});

// Configuración de multer: guarda el archivo IFC siempre con el mismo nombre
const almacenamiento = multer.diskStorage({
    destination: (req, file, cb) => cb(null, 'uploads'),
    filename: (req, file, cb) => cb(null, 'modelo-actual.ifc'),
});
const upload = multer({ storage: almacenamiento });

app.get('/', (req, res) => {
    res.send('Servidor backend funcionando correctamente');
});

// Ruta para GUARDAR una medición nueva
app.post('/mediciones', async (req, res) => {
    try {
        const { nombre_elemento, categoria, global_id, disciplina, sub_disciplina_1, sub_disciplina_2, unidad, metrado } = req.body;
        const resultado = await pool.query(
            `INSERT INTO mediciones (nombre_elemento, categoria, global_id, disciplina, sub_disciplina_1, sub_disciplina_2, unidad, metrado)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8) RETURNING *`,
            [nombre_elemento, categoria, global_id, disciplina, sub_disciplina_1, sub_disciplina_2, unidad, metrado]
        );
        res.json(resultado.rows[0]);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al guardar la medición' });
    }
});

// Ruta para OBTENER todas las mediciones
app.get('/mediciones', async (req, res) => {
    try {
        const resultado = await pool.query('SELECT * FROM mediciones ORDER BY id DESC');
        res.json(resultado.rows);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al obtener las mediciones' });
    }
});

// Ruta para BORRAR todas las mediciones (antes de una nueva sincronización,
// así siempre queda solo la información del modelo más reciente)
app.delete('/mediciones', async (req, res) => {
    try {
        await pool.query('DELETE FROM mediciones');
        res.json({ mensaje: 'Todas las mediciones anteriores fueron eliminadas' });
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al borrar las mediciones' });
    }
});

// Ruta para GUARDAR un elemento clasificado
app.post('/elementos-clasificados', async (req, res) => {
    try {
        const { familia, tipo, disciplina, subtitulo, detalles, ejes } = req.body;
        const resultado = await pool.query(
            `INSERT INTO elementos_clasificados (familia, tipo, disciplina, subtitulo, detalles, ejes)
            VALUES ($1, $2, $3, $4, $5, $6) RETURNING *`,
            [familia, tipo, disciplina, subtitulo, detalles, ejes]
        );
        res.json(resultado.rows[0]);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al guardar el elemento clasificado' });
    }
});

// Ruta para OBTENER todos los elementos clasificados
app.get('/elementos-clasificados', async (req, res) => {
    try {
        const resultado = await pool.query('SELECT * FROM elementos_clasificados ORDER BY id DESC');
        res.json(resultado.rows);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al obtener los elementos clasificados' });
    }
});

// Ruta para BORRAR todos los elementos clasificados (antes de una nueva sincronización)
app.delete('/elementos-clasificados', async (req, res) => {
    try {
        await pool.query('DELETE FROM elementos_clasificados');
        res.json({ mensaje: 'Todos los elementos clasificados anteriores fueron eliminados' });
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al borrar los elementos clasificados' });
    }
});

// Ruta para SUBIR el archivo IFC exportado desde Revit
app.post('/modelo-ifc', upload.single('archivo'), (req, res) => {
    if (!req.file) {
        return res.status(400).json({ error: 'No se recibió ningún archivo' });
    }
    res.json({ mensaje: 'Modelo IFC recibido correctamente' });
});

// Ruta para DESCARGAR el archivo IFC más reciente
app.get('/modelo-ifc', (req, res) => {
    const rutaArchivo = path.join(__dirname, 'uploads', 'modelo-actual.ifc');
    res.sendFile(rutaArchivo, (err) => {
        if (err) {
            res.status(404).json({ error: 'Todavía no se ha subido ningún modelo IFC' });
        }
    });
});

const PORT = 3000;
app.listen(PORT, () => {
    console.log(`Servidor corriendo en http://localhost:${PORT}`);
});
