const { spawn } = require('child_process');
const chokidar = require('chokidar');

let buildProcess = null;

const runBuild = () => {
  if (buildProcess) {
    console.log('🔁 Canceling previous build...');
    buildProcess.kill();
    buildProcess = null;
  }

  console.log('🚧 Starting build...');
  buildProcess = spawn('npm', ['run', 'dev'], { shell: true });

  buildProcess.stdout.on('data', (data) => {
    process.stdout.write(data);
  });

  buildProcess.stderr.on('data', (data) => {
    process.stderr.write(data);
  });

  buildProcess.on('exit', (code) => {
    if (code !== null) {
      console.log(`✅ Build finished with code ${code}`);
    }
    buildProcess = null;
  });

  buildProcess.on('error', (err) => {
    console.error(`❌ Failed to start build: ${err.message}`);
    buildProcess = null;
  });
};

chokidar
  .watch(['**/*.ts', '**/*.tsx'], {
    ignored: /node_modules/,
    ignoreInitial: true,
  })
  .on('all', (event, path) => {
    console.log(`📂 File change detected (${event}): ${path}`);
    runBuild();
  });

console.log('👀 Watching for file changes...');