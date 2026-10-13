// Usage: node render_logo.cjs [absolute path to the sharp package]
// SVG is the authoritative artwork. PNG and ICO are deployment formats.
const path = require('path');
const sharp = require(process.argv[2] || 'sharp');
const root = path.dirname(__dirname);
sharp(path.join(root, 'assets', 'logo.svg'))
  .resize(1024, 1024).png()
  .toFile(path.join(root, 'assets', 'logo-render.png'))
  .then(() => process.stdout.write('SVG rendered at 1024px.\n'))
  .catch(error => { process.stderr.write(error.message); process.exitCode = 1; });
