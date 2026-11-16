// Render the repo-native SVG cover; the selected icons retain their Game-icons attribution.
const fs=require('fs'),path=require('path');
const sharp=require(process.argv[4]||'sharp');
const root=path.resolve(__dirname,'..'),source=path.resolve(process.argv[2]),target=path.resolve(process.argv[3]);
const svg=fs.readFileSync(source,'utf8').replace(/ASSET:([a-z]+)/g,(_,name)=>'data:image/svg+xml;base64,'+fs.readFileSync(path.join(root,'assets','status-icons',name+'.svg')).toString('base64'));
sharp(Buffer.from(svg)).resize(1536,864).png({compressionLevel:9}).toFile(target).then(info=>{
  if(info.size>=1000000)throw Error('Cover exceeds Steam preview limit');
  console.log('Cover: '+info.width+'x'+info.height+', '+info.size+' bytes');
}).catch(e=>{console.error(e);process.exit(1);});
