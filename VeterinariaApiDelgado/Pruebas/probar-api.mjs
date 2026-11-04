import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const baseUrl = (process.argv[2] || 'http://127.0.0.1:5088').replace(/\/$/, '');
const target = new URL(baseUrl);
if (!['http:', 'https:'].includes(target.protocol) || target.username || target.password)
    throw new Error('Indique una URL HTTP/HTTPS sin credenciales.');
const outputDir = path.resolve(process.argv[3] || path.join(path.dirname(fileURLToPath(import.meta.url)), 'evidencias'));
const local = ['localhost', '127.0.0.1', '[::1]'].includes(target.hostname);
const results = [];
const propietarios = new Set();
const mascotas = new Set();
const suffix = Date.now().toString();
const documento = 'LAB' + suffix;
const owner = { nombre: 'Prueba laboratorio ' + suffix, documento, telefono: '900000099' };
let p1, p2, m1;

async function request(title, method, route, expected, body, headers = {}) {
    const started = Date.now();
    const row = { title, method, url: baseUrl + route, expected, request: body ?? null };
    results.push(row);
    try {
        const response = await fetch(row.url, {
            method,
            headers: { ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...headers },
            body: body === undefined ? undefined : JSON.stringify(body),
            signal: AbortSignal.timeout(90000)
        });
        const text = await response.text();
        row.status = response.status;
        row.headers = Object.fromEntries(response.headers);
        row.response = text;
        row.ms = Date.now() - started;
        row.ok = response.status === expected;
        if (!row.ok) throw new Error('Se esperaba ' + expected + ', se recibió ' + response.status + ': ' + text);
        console.log('OK ' + response.status + ' ' + title);
        let data;
        try { data = JSON.parse(text); } catch { data = text; }
        return { response, data, row };
    } catch (error) {
        row.ok = false;
        row.error = error.message;
        throw error;
    }
}
function check(result, condition, message) {
    if (!condition) {
        result.row.ok = false;
        result.row.error = message;
        throw new Error(message);
    }
}
const petBody = () => ({ nombre: 'Mascota laboratorio ' + suffix, especie: 'Perro', raza: 'Mestizo', edad: 3, pesoKg: 5.5, propietarioId: p1 });
try {
    await request('Swagger disponible', 'GET', '/swagger/index.html', 200);
    let r = await request('Documento OpenAPI', 'GET', '/openapi/v1.json', 200);
    const operations = Object.values(r.data.paths).flatMap(p => Object.keys(p).filter(k => ['get', 'post', 'put', 'delete', 'patch'].includes(k)));
    check(r, operations.length >= 12, 'OpenAPI debe exponer al menos 12 operaciones.');
    const createSchema = r.data.paths['/api/Mascotas']?.post;
    check(r, !!createSchema, 'Debe existir POST /api/Mascotas.');

    await request('GET propietarios', 'GET', '/api/propietarios', 200);
    await request('GET mascotas', 'GET', '/api/mascotas', 200);
    r = await request('Crear propietario A', 'POST', '/api/propietarios', 201, owner);
    p1 = r.data.id; propietarios.add(p1);
    check(r, Number.isInteger(p1) && r.response.headers.has('location'), 'POST propietario debe devolver Id y Location.');
    r = await request('Seguir Location propietario', 'GET', new URL(r.response.headers.get('location'), baseUrl).pathname, 200);
    check(r, r.data.documento === documento, 'El propietario guardado debe coincidir.');
    r = await request('Crear propietario B', 'POST', '/api/propietarios', 201, { ...owner, documento: 'B' + suffix });
    p2 = r.data.id; propietarios.add(p2);
    await request('Documento duplicado al crear', 'POST', '/api/propietarios', 409, owner);
    await request('Documento duplicado al actualizar', 'PUT', '/api/propietarios/' + p2, 409, { ...owner, id: p2 });
    await request('Nombre obligatorio propietario', 'POST', '/api/propietarios', 400, { ...owner, nombre: '   ' });
    await request('Documento obligatorio', 'POST', '/api/propietarios', 400, { nombre: 'Prueba', telefono: '900000099' });
    await request('Longitud máxima propietario', 'POST', '/api/propietarios', 400, { ...owner, nombre: 'X'.repeat(101) });
    await request('Id automático propietario', 'POST', '/api/propietarios', 400, { ...owner, id: 999 });
    await request('Id diferente en PUT propietario', 'PUT', '/api/propietarios/' + p1, 400, { ...owner, id: p2 });
    r = await request('Filtrar propietarios', 'GET', '/api/propietarios?documento=' + documento, 200);
    check(r, r.data.length === 1 && r.data[0].id === p1, 'El filtro por documento debe devolver solo el propietario A.');
    r = await request('Ruta anidada vacía', 'GET', '/api/propietarios/' + p1 + '/mascotas', 200);
    check(r, r.data.length === 0, 'El nuevo propietario no debe tener mascotas.');

    r = await request('Crear mascota solo con FK', 'POST', '/api/mascotas', 201, petBody());
    m1 = r.data.id; mascotas.add(m1);
    check(r, Number.isInteger(m1) && r.response.headers.has('location'), 'POST mascota debe devolver Id y Location sin requerir navegación.');
    r = await request('Seguir Location mascota', 'GET', new URL(r.response.headers.get('location'), baseUrl).pathname, 200);
    check(r, r.data.propietario?.id === p1, 'Include debe devolver el propietario sin ciclo JSON.');
    await request('Eliminar propietario con mascotas', 'DELETE', '/api/propietarios/' + p1, 409);
    r = await request('Ruta anidada con mascota', 'GET', '/api/propietarios/' + p1 + '/mascotas', 200);
    check(r, r.data.length === 1 && r.data[0].id === m1, 'La ruta anidada debe devolver la mascota creada.');
    r = await request('Filtros combinados mascotas', 'GET', '/api/mascotas?especie=Perro&propietarioId=' + p1 + '&edadMin=2&edadMax=4', 200);
    check(r, r.data.length === 1 && r.data[0].id === m1, 'Los filtros deben combinarse correctamente.');
    r = await request('Listado de especies', 'GET', '/api/mascotas/especies', 200);
    check(r, r.data.includes('Perro') && new Set(r.data).size === r.data.length, 'Especies debe devolver valores distintos.');
    await request('Rango de filtros invertido', 'GET', '/api/mascotas?edadMin=5&edadMax=2', 400);
    await request('Filtro fuera de rango', 'GET', '/api/mascotas?edadMin=-1', 400);
    await request('Filtro de tipo incorrecto', 'GET', '/api/mascotas?propietarioId=abc', 400);
    await request('FK inexistente al crear mascota', 'POST', '/api/mascotas', 400, { ...petBody(), propietarioId: 2147483647 });
    await request('Nombre obligatorio mascota', 'POST', '/api/mascotas', 400, { ...petBody(), nombre: '' });
    await request('Especie obligatoria', 'POST', '/api/mascotas', 400, { ...petBody(), especie: null });
    await request('Edad negativa', 'POST', '/api/mascotas', 400, { ...petBody(), edad: -1 });
    await request('Edad excesiva', 'POST', '/api/mascotas', 400, { ...petBody(), edad: 101 });
    await request('Peso inválido', 'POST', '/api/mascotas', 400, { ...petBody(), pesoKg: 0 });
    await request('Longitud máxima mascota', 'POST', '/api/mascotas', 400, { ...petBody(), especie: 'X'.repeat(51) });
    await request('Id diferente en PUT mascota', 'PUT', '/api/mascotas/' + m1, 400, { ...petBody(), id: m1 + 1 });
    await request('FK inexistente al actualizar mascota', 'PUT', '/api/mascotas/' + m1, 400, { ...petBody(), id: m1, propietarioId: 2147483647 });
    await request('Actualizar propietario', 'PUT', '/api/propietarios/' + p1, 204, { ...owner, id: p1, nombre: 'Propietario actualizado' });
    r = await request('Verificar actualización propietario', 'GET', '/api/propietarios/' + p1, 200);
    check(r, r.data.nombre === 'Propietario actualizado', 'PUT propietario debe persistir el nombre.');
    await request('Actualizar mascota', 'PUT', '/api/mascotas/' + m1, 204, { ...petBody(), id: m1, edad: 4, propietarioId: p2 });
    r = await request('Verificar actualización mascota', 'GET', '/api/mascotas/' + m1, 200);
    check(r, r.data.edad === 4 && r.data.propietario?.id === p2, 'PUT mascota debe persistir edad y cambio de propietario.');

    for (const entity of ['propietarios', 'mascotas']) {
        await request('GET inexistente ' + entity, 'GET', '/api/' + entity + '/2147483647', 404);
        await request('DELETE inexistente ' + entity, 'DELETE', '/api/' + entity + '/2147483647', 404);
        await request('PUT inexistente ' + entity, 'PUT', '/api/' + entity + '/2147483647', 404,
            entity === 'propietarios' ? { ...owner, id: 2147483647 } : { ...petBody(), id: 2147483647 });
    }
    await request('Ruta anidada propietario inexistente', 'GET', '/api/propietarios/2147483647/mascotas', 404);
    r = await request('CORS origen permitido', 'OPTIONS', '/api/mascotas', 204, undefined,
        { Origin: 'http://localhost:5500', 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'content-type' });
    check(r, r.response.headers.get('access-control-allow-origin') === 'http://localhost:5500', 'CORS debe autorizar el origen configurado.');
    r = await request('CORS origen no permitido', 'OPTIONS', '/api/mascotas', 204, undefined,
        { Origin: 'https://origen-no-permitido.example', 'Access-Control-Request-Method': 'POST' });
    check(r, !r.response.headers.has('access-control-allow-origin'), 'CORS no debe autorizar un origen ajeno.');

    await request('Eliminar mascota', 'DELETE', '/api/mascotas/' + m1, 204); mascotas.delete(m1);
    await request('Verificar mascota eliminada', 'GET', '/api/mascotas/' + m1, 404);
    await request('Eliminar propietario A', 'DELETE', '/api/propietarios/' + p1, 204); propietarios.delete(p1);
    await request('Eliminar propietario B', 'DELETE', '/api/propietarios/' + p2, 204); propietarios.delete(p2);
    await request('Verificar propietario eliminado', 'GET', '/api/propietarios/' + p1, 404);
} catch (error) {
    console.error('FALLO: ' + error.message);
    process.exitCode = 1;
} finally {
    // Solo se eliminan los registros creados por esta ejecución.
    for (const id of mascotas) {
        try { await request('Limpieza mascota ' + id, 'DELETE', '/api/mascotas/' + id, 204); }
        catch (error) { console.error(error.message); process.exitCode = 1; }
    }
    for (const id of propietarios) {
        try { await request('Limpieza propietario ' + id, 'DELETE', '/api/propietarios/' + id, 204); }
        catch (error) { console.error(error.message); process.exitCode = 1; }
    }
    await fs.mkdir(outputDir, { recursive: true });
    const report = { date: new Date().toISOString(), baseUrl, scope: local ? 'LOCAL' : 'URL REMOTA: comprobar que corresponde al hosting público', total: results.length, passed: results.filter(r => r.ok).length, results };
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const prefix = path.join(outputDir, 'pruebas-' + stamp);
    await fs.writeFile(prefix + '.json', JSON.stringify(report, null, 2));
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    const cards = results.map((r, i) => '<section><h2>' + (i + 1) + '. ' + esc(r.title) + '</h2><p class="' + (r.ok ? 'ok' : 'fail') + '">' + (r.ok ? 'CORRECTO' : 'FALLO') + ' · HTTP ' + esc(r.status) + ' · Esperado ' + r.expected + '</p><p>' + esc(r.method + ' ' + r.url) + '</p><h3>Solicitud</h3><pre>' + esc(JSON.stringify(r.request, null, 2)) + '</pre><h3>Respuesta</h3><pre>' + esc(r.response) + '</pre>' + (r.error ? '<p>' + esc(r.error) + '</p>' : '') + '</section>').join('');
    await fs.writeFile(prefix + '.html', '<!doctype html><html lang="es"><meta charset="utf-8"><title>Pruebas Veterinaria API</title><style>body{font:16px system-ui;max-width:1000px;margin:40px auto;padding:0 24px;color:#172336}section{border-top:1px solid #ccd3dc;padding:20px 0;break-inside:avoid}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f4f6f8;padding:16px}.ok{color:#146331}.fail{color:#ad1f23}h2{font-size:20px}h3{font-size:16px}</style><h1>Pruebas Veterinaria API</h1><p>' + esc(report.scope) + ' · ' + esc(report.date) + '</p><p>URL: ' + esc(baseUrl) + '</p><p>Correctas: ' + report.passed + '/' + report.total + '</p>' + cards + '</html>');
    console.log('Resultado: ' + report.passed + '/' + report.total + '. Evidencias: ' + prefix + '.html');
}
