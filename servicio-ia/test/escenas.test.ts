import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { extraerFragmento, normalizarEtiquetas, ubicarEscenas } from '../src/escenas.js';

const PERMITIDAS = new Set(['suspenso', 'calma', 'misterio', 'terror', 'feliz', 'intenso']);

const CAPITULO = [
  'La mañana llegó despacio sobre el pueblo. Los pájaros cantaban en los tejados y Ana preparaba café.',
  '',
  'Esa noche, en el sótano, algo arañó la puerta. Ana bajó con una vela temblando en la mano.',
  '',
  '—¿Quién anda ahí? —preguntó, pero nadie respondió desde la oscuridad del pasillo.',
  '',
  'Al amanecer todos celebraron en la plaza con música, risas y pan recién horneado.',
].join('\n');

describe('normalizarEtiquetas', () => {
  test('pasa a minúsculas, quita duplicadas y descarta las desconocidas', () => {
    assert.deepEqual(normalizarEtiquetas([' Calma', 'calma', 'alegria', 'MISTERIO'], PERMITIDAS), ['calma', 'misterio']);
  });

  test('deja como máximo tres etiquetas', () => {
    assert.deepEqual(normalizarEtiquetas(['calma', 'misterio', 'terror', 'feliz'], PERMITIDAS), [
      'calma',
      'misterio',
      'terror',
    ]);
  });

  test('"intenso" sola no es válida', () => {
    assert.deepEqual(normalizarEtiquetas(['intenso'], PERMITIDAS), []);
    assert.deepEqual(normalizarEtiquetas(['intenso', 'terror'], PERMITIDAS), ['intenso', 'terror']);
  });
});

describe('ubicarEscenas', () => {
  test('ubica fragmentos literales y numera las escenas en orden', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [
        { fragmentoInicio: 'La mañana llegó despacio sobre el pueblo.', etiquetas: ['calma'] },
        { fragmentoInicio: 'Esa noche, en el sótano, algo arañó la puerta.', etiquetas: ['suspenso', 'terror'] },
        { fragmentoInicio: 'Al amanecer todos celebraron en la plaza', etiquetas: ['feliz'] },
      ],
      PERMITIDAS,
    );

    assert.equal(escenas.length, 3);
    assert.deepEqual(
      escenas.map((escena) => escena.orden),
      [1, 2, 3],
    );
    assert.equal(escenas[0]?.posicionInicio, 0);
    assert.equal(escenas[1]?.posicionInicio, CAPITULO.indexOf('Esa noche'));
    assert.equal(escenas[2]?.posicionInicio, CAPITULO.indexOf('Al amanecer'));
    assert.deepEqual(escenas[1]?.etiquetas, ['suspenso', 'terror']);
    assert.ok(escenas[1]?.fragmentoInicio.startsWith('Esa noche, en el sótano'));
  });

  test('tolera tildes, mayúsculas, espacios y puntos suspensivos distintos', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [
        { fragmentoInicio: 'la manana llego despacio', etiquetas: ['calma'] },
        { fragmentoInicio: 'ESA NOCHE,   en el sotano, algo arano la puerta…', etiquetas: ['terror'] },
      ],
      PERMITIDAS,
    );
    assert.equal(escenas.length, 2);
    assert.equal(escenas[1]?.posicionInicio, CAPITULO.indexOf('Esa noche'));
  });

  test('incluye la raya de diálogo aunque la IA la omita', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [
        { fragmentoInicio: 'La mañana llegó despacio', etiquetas: ['calma'] },
        { fragmentoInicio: '¿Quién anda ahí? —preguntó, pero nadie respondió', etiquetas: ['misterio'] },
      ],
      PERMITIDAS,
    );
    assert.equal(escenas[1]?.posicionInicio, CAPITULO.indexOf('—¿Quién'));
  });

  test('usa el inicio del fragmento cuando la IA cambia las últimas palabras', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [
        { fragmentoInicio: 'La mañana llegó despacio', etiquetas: ['calma'] },
        { fragmentoInicio: 'Al amanecer todos celebraron felices en el parque', etiquetas: ['feliz'] },
      ],
      PERMITIDAS,
    );
    assert.equal(escenas[1]?.posicionInicio, CAPITULO.indexOf('Al amanecer'));
  });

  test('descarta escenas sin etiquetas válidas, no encontradas o fuera de orden', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [
        { fragmentoInicio: 'Esa noche, en el sótano, algo arañó la puerta.', etiquetas: ['terror'] },
        { fragmentoInicio: 'La mañana llegó despacio sobre el pueblo.', etiquetas: ['calma'] },
        { fragmentoInicio: 'Un dragón aterrizó sobre la torre del castillo', etiquetas: ['terror'] },
        { fragmentoInicio: 'Al amanecer todos celebraron en la plaza', etiquetas: ['intenso'] },
        { fragmentoInicio: 'Al amanecer todos celebraron en la plaza', etiquetas: ['feliz'] },
      ],
      PERMITIDAS,
    );
    assert.equal(escenas.length, 2);
    assert.equal(escenas[0]?.posicionInicio, 0);
    assert.deepEqual(escenas[0]?.etiquetas, ['terror']);
    assert.equal(escenas[1]?.posicionInicio, CAPITULO.indexOf('Al amanecer'));
  });

  test('la primera escena siempre empieza en la posición 0', () => {
    const escenas = ubicarEscenas(
      CAPITULO,
      [{ fragmentoInicio: 'Esa noche, en el sótano, algo arañó la puerta.', etiquetas: ['terror'] }],
      PERMITIDAS,
    );
    assert.equal(escenas.length, 1);
    assert.equal(escenas[0]?.posicionInicio, 0);
    assert.ok(escenas[0]?.fragmentoInicio.startsWith('La mañana'));
  });

  test('devuelve una lista vacía si nada coincide', () => {
    assert.deepEqual(ubicarEscenas(CAPITULO, [{ fragmentoInicio: 'Texto inexistente en el capítulo', etiquetas: ['calma'] }], PERMITIDAS), []);
  });
});

describe('extraerFragmento', () => {
  test('recorta en un límite de palabra sin pasar de 80 caracteres', () => {
    const fragmento = extraerFragmento(CAPITULO, 0);
    assert.ok(fragmento.length <= 80);
    assert.ok(CAPITULO.startsWith(fragmento));
    assert.ok(!fragmento.endsWith(' '));
  });

  test('une los saltos de línea en un solo espacio', () => {
    assert.equal(extraerFragmento('Hola\n\n  mundo', 0), 'Hola mundo');
  });
});
