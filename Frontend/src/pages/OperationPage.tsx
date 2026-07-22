import { useCallback, useEffect, useState } from 'react'
import api from '../services/api'
import { Building, Central, CentralStatus, SessaoInfo, PgmPredio, ZonaPredio } from '../types'
import StatusConexaoCard from '../components/session/StatusConexaoCard'
import LogCentralPanel from '../components/session/LogCentralPanel'
import ArmPanel from '../components/ArmPanel'
import PgmPanel from '../components/PgmPanel'
import ZonasPanel from '../components/ZonasPanel'
import CadastroPgmZonaPanel from '../components/CadastroPgmZonaPanel'
import {
  Box, Typography, MenuItem, Select, InputLabel, FormControl, Stack, Alert
} from '@mui/material'

const INTERVALO_STATUS_MS = 5000
const INTERVALO_SESSAO_MS = 5000

/**
 * Painel operacional dinâmico: o operador escolhe Prédio → Central, e tudo mais (status,
 * PGMs cadastradas, Zonas cadastradas, Arme/Desarme, log) carrega automaticamente. Nenhum
 * número de PGM/Zona é digitado manualmente — os cartões vêm do cadastro (PgmPredio/ZonaPredio),
 * e todo comando reaproveita os mesmos endpoints/serviços reais já usados pela Tela Central
 * (PgmService, ArmService, ZoneInhibitService, CentralStatusService) — nenhuma lógica nova de
 * protocolo vive aqui.
 */
export default function OperationPage() {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [centrals, setCentrals] = useState<Central[]>([])
  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [centralId, setCentralId] = useState<number | ''>('')

  const [status, setStatus] = useState<CentralStatus | null>(null)
  const [statusErro, setStatusErro] = useState<string | null>(null)
  const [carregandoStatus, setCarregandoStatus] = useState(false)

  const [sessao, setSessao] = useState<SessaoInfo | null>(null)
  const [carregandoSessao, setCarregandoSessao] = useState(false)

  const [pgmsCatalogo, setPgmsCatalogo] = useState<PgmPredio[]>([])
  const [zonasCatalogo, setZonasCatalogo] = useState<ZonaPredio[]>([])

  useEffect(() => {
    api.get<Building[]>('/building').then(r => setBuildings(r.data))
    api.get<Central[]>('/central').then(r => setCentrals(r.data))
  }, [])

  const centralsDoPredio = centrals.filter(c => c.buildingId === buildingId)

  // Ao escolher um Prédio, se ele só tiver uma Central, seleciona automaticamente.
  useEffect(() => {
    setCentralId('')
    if (buildingId && centralsDoPredio.length === 1) {
      setCentralId(centralsDoPredio[0].id)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [buildingId])

  const carregarStatus = useCallback(async () => {
    if (!centralId) return
    setCarregandoStatus(true)
    try {
      const response = await api.get<CentralStatus>(`/centrais/${centralId}/status`)
      setStatus(response.data)
      setStatusErro(null)
    } catch (e) {
      setStatus(null)
      const resposta = (e as { response?: { data?: { mensagem?: string } } }).response
      setStatusErro(resposta?.data?.mensagem || 'Não foi possível consultar o status da central.')
    } finally {
      setCarregandoStatus(false)
    }
  }, [centralId])

  const carregarSessao = useCallback(async () => {
    if (!centralId) return
    setCarregandoSessao(true)
    try {
      const response = await api.get<SessaoInfo>(`/centrais/${centralId}/sessao`)
      setSessao(response.data)
    } catch {
      setSessao(null)
    } finally {
      setCarregandoSessao(false)
    }
  }, [centralId])

  const carregarCatalogo = useCallback(async () => {
    if (!buildingId || !centralId) return
    const [pgmsResp, zonasResp] = await Promise.all([
      api.get<PgmPredio[]>('/pgmpredio', { params: { buildingId, centralId } }),
      api.get<ZonaPredio[]>('/zonapredio', { params: { buildingId, centralId } }),
    ])
    setPgmsCatalogo(pgmsResp.data)
    setZonasCatalogo(zonasResp.data)
  }, [buildingId, centralId])

  useEffect(() => {
    if (!centralId) {
      setStatus(null)
      setSessao(null)
      setPgmsCatalogo([])
      setZonasCatalogo([])
      return
    }

    carregarStatus()
    carregarSessao()
    carregarCatalogo()
    const idStatus = setInterval(carregarStatus, INTERVALO_STATUS_MS)
    const idSessao = setInterval(carregarSessao, INTERVALO_SESSAO_MS)
    return () => {
      clearInterval(idStatus)
      clearInterval(idSessao)
    }
  }, [centralId, carregarStatus, carregarSessao, carregarCatalogo])

  const zonasAtivas = status ? status.zonas.filter(z => z.estado !== 'Desabilitada') : []

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Operação</Typography>

      <Stack spacing={2} sx={{ mb: 3, maxWidth: 500 }}>
        <FormControl>
          <InputLabel>Prédio</InputLabel>
          <Select label="Prédio" value={buildingId} onChange={e => setBuildingId(e.target.value as number)}>
            {buildings.map(b => (
              <MenuItem key={b.id} value={b.id}>{b.nome}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl disabled={!buildingId}>
          <InputLabel>Central</InputLabel>
          <Select label="Central" value={centralId} onChange={e => setCentralId(e.target.value as number)}>
            {centralsDoPredio.map(c => (
              <MenuItem key={c.id} value={c.id}>{c.nome}</MenuItem>
            ))}
            {buildingId && centralsDoPredio.length === 0 && (
              <MenuItem value="" disabled>Nenhuma central neste prédio</MenuItem>
            )}
          </Select>
        </FormControl>
      </Stack>

      {!centralId && (
        <Alert severity="info">Selecione um Prédio e uma Central para carregar o painel operacional.</Alert>
      )}

      {centralId && typeof buildingId === 'number' && (
        <>
          <StatusConexaoCard
            sessao={sessao}
            carregando={carregandoSessao}
            aguardandoConexao={false}
            onAtualizar={carregarSessao}
          />

          {statusErro && <Alert severity="warning" sx={{ mb: 2 }}>{statusErro}</Alert>}

          <ArmPanel
            centralId={centralId}
            particoes={status?.particoes ?? []}
            eletrificador={status?.eletrificador}
            onComandoConcluido={carregarStatus}
          />

          <Box sx={{ mb: 3 }}>
            <PgmPanel
              centralId={centralId}
              pgms={status?.pgms ?? []}
              catalogo={pgmsCatalogo}
              onComandoConcluido={carregarStatus}
            />
          </Box>

          <Box sx={{ mb: 3, p: 2, bgcolor: 'background.paper', border: 1, borderColor: 'divider', borderRadius: 1 }}>
            <ZonasPanel
              zonas={zonasAtivas}
              totalZonas={status?.zonas.length ?? 0}
              centralId={centralId}
              catalogo={zonasCatalogo}
              onComandoConcluido={carregarStatus}
            />
          </Box>

          <LogCentralPanel centralId={centralId} />

          <CadastroPgmZonaPanel
            buildingId={buildingId}
            centralId={centralId}
            onCatalogoAlterado={carregarCatalogo}
          />
        </>
      )}
    </Box>
  )
}
