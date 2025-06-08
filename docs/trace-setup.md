# how to setup local environment to test trace

## 1. start the SRE agent locally in development mode 

You can start through Visual Studio

## 2. start the DedugApp backend

You can start through Visual Studio

## 3. start the DebugApp Frontend

```
cd AAPT-SREAgent-ControlPlane\src\DebugApp\Client
npm install
npm run dev
```

## 4. go back to DebugApp dashboard to search thread-id 

url is http://127.0.0.1:3000, you can search through thread-id, without agent name.