-- =============================================================
-- Immersic Reader - Esquema de base de datos (SQLite)
-- -------------------------------------------------------------
-- La API ejecuta este script cada vez que inicia. Todas las
-- sentencias son idempotentes: crean lo que falte y no modifican
-- datos existentes.
-- =============================================================

PRAGMA foreign_keys = ON;

-- -------------------------------------------------------------
-- Libros
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS libros (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo     TEXT    NOT NULL CHECK (length(titulo) BETWEEN 1 AND 150),
    autor      TEXT             CHECK (autor IS NULL OR length(autor) <= 100),
    creado_en  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now'))
);

-- -------------------------------------------------------------
-- Capitulos (1 libro : N capitulos)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS capitulos (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    libro_id      INTEGER NOT NULL REFERENCES libros (id) ON DELETE CASCADE,
    numero        INTEGER NOT NULL CHECK (numero >= 1),
    titulo        TEXT    NOT NULL CHECK (length(titulo) BETWEEN 1 AND 150),
    contenido     TEXT    NOT NULL DEFAULT '' CHECK (length(contenido) <= 60000),
    procesado_ia  INTEGER NOT NULL DEFAULT 0 CHECK (procesado_ia IN (0, 1)),
    creado_en     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT uq_capitulos_libro_numero UNIQUE (libro_id, numero)
);

-- -------------------------------------------------------------
-- Escenas (1 capitulo : N escenas)
-- posicion_inicio: indice del caracter del contenido donde
-- empieza la escena. La escena termina donde empieza la siguiente.
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS escenas (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    capitulo_id       INTEGER NOT NULL REFERENCES capitulos (id) ON DELETE CASCADE,
    orden             INTEGER NOT NULL CHECK (orden >= 1),
    posicion_inicio   INTEGER NOT NULL CHECK (posicion_inicio >= 0),
    fragmento_inicio  TEXT    NOT NULL,
    CONSTRAINT uq_escenas_capitulo_posicion UNIQUE (capitulo_id, posicion_inicio)
);

-- -------------------------------------------------------------
-- Etiquetas de emocion (catalogo)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS etiquetas (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre       TEXT    NOT NULL COLLATE NOCASE CHECK (length(nombre) BETWEEN 1 AND 30),
    descripcion  TEXT    NOT NULL DEFAULT '' CHECK (length(descripcion) <= 300),
    CONSTRAINT uq_etiquetas_nombre UNIQUE (nombre)
);

-- -------------------------------------------------------------
-- Pistas de musica
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS pistas (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre       TEXT    NOT NULL CHECK (length(nombre) BETWEEN 1 AND 150),
    artista      TEXT             CHECK (artista IS NULL OR length(artista) <= 100),
    archivo_url  TEXT             CHECK (archivo_url IS NULL OR length(archivo_url) <= 500)
);

-- -------------------------------------------------------------
-- Escena <-> Etiqueta (N:M, de 1 a 3 etiquetas por escena)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS escena_etiquetas (
    escena_id    INTEGER NOT NULL REFERENCES escenas (id)   ON DELETE CASCADE,
    etiqueta_id  INTEGER NOT NULL REFERENCES etiquetas (id) ON DELETE RESTRICT,
    PRIMARY KEY (escena_id, etiqueta_id)
);

-- -------------------------------------------------------------
-- Pista <-> Etiqueta (N:M, de 1 a 3 etiquetas por pista)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS pista_etiquetas (
    pista_id     INTEGER NOT NULL REFERENCES pistas (id)    ON DELETE CASCADE,
    etiqueta_id  INTEGER NOT NULL REFERENCES etiquetas (id) ON DELETE RESTRICT,
    PRIMARY KEY (pista_id, etiqueta_id)
);

CREATE INDEX IF NOT EXISTS ix_escena_etiquetas_etiqueta ON escena_etiquetas (etiqueta_id);
CREATE INDEX IF NOT EXISTS ix_pista_etiquetas_etiqueta  ON pista_etiquetas (etiqueta_id);

-- -------------------------------------------------------------
-- Catalogo inicial de etiquetas (solo si la tabla esta vacia)
-- -------------------------------------------------------------
INSERT INTO etiquetas (nombre, descripcion)
SELECT column1, column2
FROM (VALUES
    ('suspenso', 'Preocupación, tensión baja pero constante, investigación o peligro inminente.'),
    ('accion',   'Ritmo acelerado, percusión fuerte, persecuciones o combates.'),
    ('calma',    'Música suave, paisajes relajantes o momentos pacíficos.'),
    ('misterio', 'Melodías enigmáticas, intriga, secretos revelados o caminatas nocturnas.'),
    ('perdida',  'Reflexión, luto o una derrota inmensa.'),
    ('romance',  'Conexión entre personas, amor en el aire, momentos bellos o únicos.'),
    ('terror',   'Miedo en general, a lo desconocido o a traumas, y ambientes pesados.'),
    ('epico',    'Un gran momento, la unión, la señal de un final o un momento especial.'),
    ('feliz',    'Tonos festivos, encuentros calurosos, victorias o humor.'),
    ('triste',   'Melodías lentas, melancolía, pérdidas o reflexiones profundas.'),
    ('combate',  'Más que acción: el combate en sí, de cualquier tipo.'),
    ('intenso',  'Refuerza otra emoción cuando amerita más peso. Nunca va sola.')
)
WHERE NOT EXISTS (SELECT 1 FROM etiquetas);
