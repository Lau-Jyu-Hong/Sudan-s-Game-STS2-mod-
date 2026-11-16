// Material conversion only: no AI art generation or gameplay edits.
// Usage: node tools/prepare_status_icons.cjs <game-icons checkout> <sharp module>
const fs = require('fs');
const path = require('path');
const sharp = require(process.argv[3] || 'sharp');
const root = path.resolve(__dirname, '..');
const dest = path.join(root, 'assets', 'status-icons');
const upstream = path.resolve(process.argv[2]);
const mapping = JSON.parse(fs.readFileSync(path.join(dest, 'mapping.json'), 'utf8'));
const colors = {gold:'#edc66f',silver:'#cedbe5',bronze:'#dfa875',red:'#ee8c83',purple:'#c2a0eb',blue:'#9bd7ed',green:'#a6d9a1'};
async function run() {
  for (const [key, [label, source, tone]] of Object.entries(mapping)) {
    const original = fs.readFileSync(path.join(upstream, source + '.svg'), 'utf8');
    const rawPath = path.join(dest, 'source', source + '.svg');
    fs.mkdirSync(path.dirname(rawPath), {recursive:true});
    fs.writeFileSync(rawPath, original);
    const styled = original.replace(/<path d="M0 0h512v512H0z"\s*\/>/, '')
      .replace(/#fff\b/gi, colors[tone]);
    const background = '<rect x="4" y="4" width="504" height="504" rx="90" fill="#20252c" stroke="'+colors[tone]+'" stroke-opacity="0.55" stroke-width="10"/>';
    // Leave breathing room around the source silhouette; preserve the original copy.
    const contents = styled.replace(/^<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
    const svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">'+background+'<g transform="translate(40 40) scale(0.84375)">'+contents+'</g></svg>';
    fs.writeFileSync(path.join(dest,key.toLowerCase()+'.svg'),svg);
    await sharp(Buffer.from(svg)).resize(128,128).png().toFile(path.join(dest,key.toLowerCase()+'.png'));
  }
  fs.copyFileSync(path.join(upstream,'license.txt'),path.join(dest,'UPSTREAM-LICENSE.txt'));
  console.log('Converted '+Object.keys(mapping).length+' icons.');
}
run().catch(e=>{console.error(e);process.exit(1);});
