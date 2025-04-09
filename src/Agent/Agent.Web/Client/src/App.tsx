import './App.css'
// import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import Activities from './src/Space/Activities/Activities.ReactView';

const App: React.FC = () => {

  // return (
  //     <Router>
  //       <Routes>
  //         <Route path="/" element={<Activities />} />
  //       </Routes>
  //   </Router>
  // )

  return (<Activities />);
}

export default App
