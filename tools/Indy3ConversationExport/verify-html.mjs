import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { Script } from 'node:vm';
import assert from 'node:assert/strict';

const path = process.argv[2] ?? 'Games/indy3/indy3-conversations.html';
const html = readFileSync(path, 'utf8');
const decode = value => value.replace(/&#(?:x([0-9a-f]+)|(\d+));|&(amp|lt|gt|quot|apos);/gi,
  (_, hex, decimal, named) => hex || decimal
    ? String.fromCodePoint(parseInt(hex ?? decimal, hex ? 16 : 10))
    : ({ amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" })[named.toLowerCase()]);
const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
assert.equal(new Set(ids).size, ids.length, 'Duplicate HTML ids');
const idSet = new Set(ids);
const links = [...html.matchAll(/href="#([^"]+)"/g)].map(match => decode(match[1]));
for (const link of links) assert.ok(idSet.has(link), `Broken internal link: ${link}`);
assert.ok(links.length > 0, 'No conversation continuation links');
assert.ok(!/(?:src|href)=["'](?:https?:|\/\/)/i.test(html), 'External dependency in offline HTML');
const images = [...html.matchAll(/src="data:image\/bmp;base64,([^"]+)"/g)];
assert.ok(images.length > 0, 'No embedded room images');
for (const image of images) {
  const encoded = image[1];
  const bitmap = Buffer.from(encoded, 'base64');
  assert.equal(bitmap.toString('ascii', 0, 2), 'BM');
  assert.equal(bitmap.readInt32LE(2), bitmap.length, 'Truncated bitmap');
  const width = bitmap.readInt32LE(18), height = bitmap.readInt32LE(22);
  assert.ok(width > 0 && height > 0);
  assert.equal(bitmap.readUInt16LE(28), 8, 'Unexpected EGA bitmap depth');
  const start = bitmap.readInt32LE(10), stride = (width + 3) & ~3;
  assert.equal(bitmap.length - start, stride * height, 'Invalid bitmap pixel size');
  assert.ok(bitmap.subarray(start).some(pixel => pixel !== 0),
    `Blank room image near ${html.slice(Math.max(0, image.index - 180), image.index)}`);
}
const scripts = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/gi)];
assert.ok(scripts.length > 0, 'Missing interactive controls');
for (const [index, script] of scripts.entries()) new Script(script[1], { filename: `${path}:script${index}` });
const text = decode(html);
assert.ok(text.includes('Hola. Vendo preciosas cazadoras de cuero como la que llevo.'), 'Missing known leather-jacket choice');
assert.ok(html.includes('aria-label="Search conversations"'), 'Search has no accessible name');
const graphPath = path.replace(/\.html$/i, '.graph.json');
const graph = JSON.parse(readFileSync(graphPath, 'utf8'));
const nodes = new Map(graph.included.map(node => [node.Id, node]));
assert.equal(nodes.size, graph.included.length, 'Duplicate menu graph ids');
for (const node of nodes.values()) for (const option of node.Options) {
  assert.ok(text.includes(option.Text), `Choice missing from HTML: ${option.Text}`);
  for (const label of option.AlternativeLabels) assert.ok(text.includes(label), `Variant missing from HTML: ${label}`);
  for (const edge of option.NextMenus) assert.ok(nodes.has(edge.TargetId), `Missing graph target ${edge.TargetId}`);
}
// A handler can live after later menus in storage: this Marcus response jumps
// backwards into shared dialogue and must reach the third menu.
const marcus = graph.included.filter(node => node.ScriptId === 29).sort((a,b) => a.StartOffset - b.StartOffset);
const distantHandler = marcus[0]?.Options.find(option => option.VerbId === 122);
assert.ok(distantHandler?.Replies.some(reply => reply.CodeOffset === 0x038f), 'Missing distant/shared Marcus reply');
assert.ok(distantHandler?.NextMenus.some(edge => edge.TargetId === marcus[2]?.Id), 'Missing Marcus menu 1 -> 3 branch');
assert.deepEqual(marcus[0].Options.find(option => option.VerbId === 121).NextMenus.map(edge => edge.TargetId),
  [marcus[1].Id], 'Marcus choice 121 must stop at menu 2, not borrow later handlers');
assert.deepEqual(marcus[2].Options.find(option => option.VerbId === 120).Replies.map(reply => reply.CodeOffset),
  [0x04c6, 0x0269, 0x057a], 'Shared backwards-jump replies must retain execution order');
assert.ok(marcus[0].Prelude.length > 0 && marcus[1].Prelude.length === 0 && marcus[2].Prelude.length === 0,
  'Opening conversation speech must not repeat at nested menus');
const guard = graph.included.filter(node => node.ScriptId === 95).sort((a,b) => a.StartOffset - b.StartOffset)[0];
const offer = guard.Options.find(option => option.VerbId === 124);
assert.ok(offer.Replies.some(reply => reply.CodeOffset === 0x1855),
  'Offering an object must expose the unsigned-pass reply after the selection resets');
assert.ok(offer.Replies.some(reply => reply.CodeOffset === 0x1881),
  'Offering an object must expose the signed-pass reply after the selection resets');
const auditPath = join(dirname(path), 'indy3-conversation-audit.json');
if (existsSync(auditPath)) {
  const audit = JSON.parse(readFileSync(auditPath, 'utf8'));
  const accounted = new Set([...nodes.keys(), ...graph.excluded.map(node => node.Id)]);
  for (const group of audit.groups) assert.ok(accounted.has(group.id), `Unaccounted choice set: ${group.id}`);
  const labels = new Set(graph.included.flatMap(node => node.Options.flatMap(option => [option.Text, ...option.AlternativeLabels])));
  const scripts = new Set(graph.included.map(node => node.ScriptId));
  for (const label of audit.orphanLabels.filter(label => scripts.has(label.ScriptId)))
    assert.ok(labels.has(label.Text), `Missing conditional label: ${label.Text}`);
}
console.log(JSON.stringify({ file: path, uniqueIds: ids.length, validContinuationLinks: links.length,
  embeddedImages: images.length, distinctRoomImages: new Set(images.map(image => image[1])).size,
  parsedScripts: scripts.length, graphMenus: nodes.size, excludedUtilityMenus: graph.excluded.length, offline: true }, null, 2));
