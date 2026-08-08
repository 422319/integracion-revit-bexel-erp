const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');

const app = express();
app.use(cors());
app.use(express.json());

const pool = new Pool({
    user: 'postgres',
    host: 'localhost',
    database: 'bim_integracion',
    password: '4223',
    port: 5432,
});

app.get('/', (req, res) => {
    res.send('Servidor backend funcionando correctamente');
});

// Ruta para GUARDAR una medición nueva
app.post('/mediciones', async (req, res) => {
    try {
        const { nombre_elemento, categoria, volumen, area } = req.body;
        const resultado = await pool.query(
            `INSERT INTO mediciones (nombre_elemento, categoria, volumen, area)
             VALUES ($1, $2, $3, $4) RETURNING *`,
            [nombre_elemento, categoria, volumen, area]
        );
        res.json(resultado.rows[0]);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al guardar la medición' });
    }
});

// Ruta para CONSULTAR todas las mediciones
app.get('/mediciones', async (req, res) => {
    try {
        const resultado = await pool.query(
            'SELECT * FROM mediciones ORDER BY fecha_sincronizacion DESC'
        );
        res.json(resultado.rows);
    } catch (error) {
        console.error(error);
        res.status(500).json({ error: 'Error al consultar las mediciones' });
    }
});

const PORT = 3000;
app.listen(PORT, () => {
    console.log(`Servidor corriendo en http://localhost:${PORT}`);
});