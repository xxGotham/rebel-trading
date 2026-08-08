import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Brief } from './pages/Brief'
import { Performance } from './pages/Performance'
import { Portfolio } from './pages/Portfolio'
import { RunDetailPage, RunList } from './pages/Runs'

const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <Brief /> },
      { path: 'runs', element: <RunList /> },
      { path: 'runs/:id', element: <RunDetailPage /> },
      { path: 'performance', element: <Performance /> },
      { path: 'portfolio', element: <Portfolio /> },
    ],
  },
])

export function App() {
  return <RouterProvider router={router} />
}
