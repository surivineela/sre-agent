const { exec } = require('child_process');
const chokidar = require('chokidar');

// Run initial build
console.log('Running initial build...');
exec('npm run dev', { cwd: __dirname }, (error, stdout, stderr) => {
  if (error) {
    console.error(`Error during initial build: ${error.message}`);
    return;
  }
  if (stderr) {
    console.error(`stderr: ${stderr}`);
    return;
  }
  console.log(`Initial build complete: ${stdout}`);
});

// Watch for changes and rebuild
const watcher = chokidar.watch(['src/**/*', 'index.html'], {
  ignored: /(^|[\/\\])\../, // ignore dotfiles
  persistent: true,
});

console.log('Watching for file changes...');

let buildInProgress = false;
let pendingBuild = false;

function runBuild() {
  if (buildInProgress) {
    pendingBuild = true;
    return;
  }
  
  buildInProgress = true;
  console.log('Change detected, rebuilding...');
  
  exec('npm run dev', { cwd: __dirname }, (error, stdout, stderr) => {
    buildInProgress = false;
    
    if (error) {
      console.error(`Error during build: ${error.message}`);
    } else {
      console.log(`Build complete: ${stdout}`);
    }
    
    if (pendingBuild) {
      pendingBuild = false;
      runBuild();
    }
  });
}

watcher
  .on('change', path => {
    console.log(`File ${path} has been changed`);
    runBuild();
  })
  .on('add', path => {
    console.log(`File ${path} has been added`);
    runBuild();
  })
  .on('unlink', path => {
    console.log(`File ${path} has been removed`);
    runBuild();
  });
