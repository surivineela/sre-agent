import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.tsx';
import { BrowserRouter } from 'react-router-dom';
import { UrlParamsProvider } from './Context/UrlParamsProvider'; // Import the new provider


ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <UrlParamsProvider> 
      <BrowserRouter basename='/static'>
        <App />
      </BrowserRouter>
    </UrlParamsProvider>
  </React.StrictMode>
);
