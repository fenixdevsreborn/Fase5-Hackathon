import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { CampaignDetailPage } from './pages/CampaignDetailPage'
import { CampaignFormPage } from './pages/CampaignFormPage'
import { CampaignsPage } from './pages/CampaignsPage'
import { DonatePage } from './pages/DonatePage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { ManagementPage } from './pages/ManagementPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RegisterPage } from './pages/RegisterPage'

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<HomePage />} />
        <Route path="campanhas" element={<CampaignsPage />} />
        <Route path="campanhas/:id" element={<CampaignDetailPage />} />
        <Route path="entrar" element={<LoginPage />} />
        <Route path="cadastro" element={<RegisterPage />} />
        <Route element={<ProtectedRoute role="Doador" />}>
          <Route path="campanhas/:id/doar" element={<DonatePage />} />
        </Route>
        <Route element={<ProtectedRoute role="GestorONG" />}>
          <Route path="gestao" element={<ManagementPage />} />
          <Route path="gestao/campanhas/nova" element={<CampaignFormPage />} />
          <Route path="gestao/campanhas/:id/editar" element={<CampaignFormPage />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
