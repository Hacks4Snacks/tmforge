import { ReactFlowProvider } from '@xyflow/react';
import { Editor } from './dfd/Editor';
import './App.css';

export function App() {
  return (
    <ReactFlowProvider>
      <Editor />
    </ReactFlowProvider>
  );
}
