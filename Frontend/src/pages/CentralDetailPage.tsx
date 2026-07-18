import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import api from '../services/api'
import { Central, CentralStatus, SessaoInfo, DiagnosticoResultado, ReconectarResultado } from '../types'
import PgmPanel from '../components/PgmPanel'
import ArmPanel from '../components/ArmPanel'
import ZonasPanel from '../components/ZonasPanel'
import StatusConexaoCard from '../components/session/StatusConexaoCard'
import SessaoTcpPanel from '../components/session/SessaoTcpPanel'
import LogCentralPanel from '../components/session/LogCentralPanel'
import DetalhesSessaoModal from '../components/session/DetalhesSessaoModal'
import DiagnosticoPanel from '../components/session/DiagnosticoPanel'
import { formatarDataHora } from '../utils/formatters'
import {
  Paper, Typography, Grid, Chip, Stack, Box, Alert, Button, CircularProgress, Tooltip,
  Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import SyncIcon from '@mui/icons-material/Sync'
import LinkOffIcon from '@mui/icons-material/LinkOff'

const INTERVALO_STATUS_MS = 5000
const INTERVALO_CENTRAL_MS = 15000
const INTERVALO_SESSAO_MS = 5000
const DURACAO_AGUARDANDO_CONEXAO_MS = 8000

const COR_PARTICAO: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Desarmada: 'default',
  Armada: 'success',
  ArmadaStay: 'success',
  DesarmadaEmDisparo: 'error',
  ArmadaEmDisparo: 'error',
  ArmadaStayEmDisparo: 'error',
}

export default function CentralDetailPage() {
  const { id } = useParams<{ id: string }>()
  const centralId = Number(id)

  const [central, setCentral] = useState<Central | null>(null)
  const [status, setStatus] = useState<CentralStatus | null>(null)
  const [statusErro, setStatusErro] = useState<string | null>(null)
  const [carregandoStatus, setCarregandoStatus] = useState(false)

  const [sessao, setSessao] = useState<SessaoInfo | null>(null)
  const [carregandoSessao, setCarregandoSessao] = useState(false)
  const [diagnostico, setDiagnostico] = useState<DiagnosticoResultado | null>(null)

  const [detalhesAbertos, setDetalhesAbertos] = useState(false)
  const [reconectarDialogAberto, setReconectarDialogAberto] = useState(false)
  const [reconectando, setReconectando] = useState(false)
  const [mensagemReconectar, setMensagemReconectar] = useState<string | null>(null)
  const [aguardandoConexao, setAguardandoConexao] = useState(false)

  const timeoutAguardandoRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const carregarCentral = useCallback(async () => {
    try {
      const response = await api.get<Central>(`/central/${centralId}`)
      setCentral(response.data)
    } catch {
      // a pagina ja sinaliza problemas via statusErro; a central em si raramente falha
    }
  }, [centralId])

  const carregarStatus = useCallback(async () => {
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
    setCarregandoSessao(true)
    try {
      const response = await api.get<SessaoInfo>(`/centrais/${centralId}/sessao`)
      setSessao(response.data)
      if (response.data.statusConexao === 'Online') {
        setAguardandoConexao(false)
      }
    } catch {
      setSessao(null)
    } finally {
      setCarregandoSessao(false)
    }
  }, [centralId])

  const carregarDiagnostico = useCallback(async () => {
    try {
      const response = await api.get<DiagnosticoResultado>(`/centrais/${centralId}/diagnostico`)
      setDiagnostico(response.data)
    } catch {
      setDiagnostico(null)
    }
  }, [centralId])

  useEffect(() => {
    carregarCentral()
    carregarStatus()
    carregarSessao()
    carregarDiagnostico()
    const idCentral = setInterval(carregarCentral, INTERVALO_CENTRAL_MS)
    const idStatus = setInterval(carregarStatus, INTERVALO_STATUS_MS)
    const idSessao = setInterval(() => { carregarSessao(); carregarDiagnostico() }, INTERVALO_SESSAO_MS)
    return () => {
      clearInterval(idCentral)
      clearInterval(idStatus)
      clearInterval(idSessao)
      if (timeoutAguardandoRef.current) {
        clearTimeout(timeoutAguardandoRef.current)
      }
    }
  }, [carregarCentral, carregarStatus, carregarSessao, carregarDiagnostico])

  const confirmarReconectar = async () => {
    setReconectando(true)
    try {
      const response = await api.post<ReconectarResultado>(`/centrais/${centralId}/reconectar`)
      setMensagemReconectar(response.data.mensagem)
      setAguardandoConexao(true)
      if (timeoutAguardandoRef.current) {
        clearTimeout(timeoutAguardandoRef.current)
      }
      timeoutAguardandoRef.current = setTimeout(() => setAguardandoConexao(false), DURACAO_AGUARDANDO_CONEXAO_MS)
      await carregarSessao()
    } catch {
      setMensagemReconectar('Não foi possível processar o pedido de reconexão.')
    } finally {
      setReconectando(false)
      setReconectarDialogAberto(false)
    }
  }

  const zonasVisiveis = useMemo(
    () => (status ? status.zonas.filter(z => z.estado !== 'Desabilitada') : []),
    [status]
  )

  if (!central) {
    return <Typography>Carregando…</Typography>
  }

  return (
    <Box>
      <Button component={Link} to="/centrais" startIcon={<ArrowBackIcon />} sx={{ mb: 2 }}>
        Voltar para Centrais
      </Button>

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" alignItems="center" spacing={1.5}>
          <Typography variant="h5">{central.nome}</Typography>
        </Stack>
      </Paper>

      {mensagemReconectar && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setMensagemReconectar(null)}>
          {mensagemReconectar}
        </Alert>
      )}

      <StatusConexaoCard
        sessao={sessao}
        carregando={carregandoSessao}
        aguardandoConexao={aguardandoConexao}
        onAtualizar={carregarSessao}
      />

      <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
        <Button
          variant="outlined"
          startIcon={<InfoOutlinedIcon />}
          disabled={!sessao}
          onClick={() => setDetalhesAbertos(true)}
        >
          Detalhes da Sessão
        </Button>
        <Button
          variant="outlined"
          startIcon={<SyncIcon />}
          onClick={carregarStatus}
          disabled={carregandoStatus}
        >
          Solicitar Status
        </Button>
        <Button
          variant="outlined"
          color="warning"
          startIcon={<LinkOffIcon />}
          onClick={() => setReconectarDialogAberto(true)}
        >
          Reconectar
        </Button>
      </Stack>

      {sessao && <SessaoTcpPanel sessao={sessao} />}

      <DiagnosticoPanel diagnostico={diagnostico} />

      <LogCentralPanel centralId={centralId} />

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 2 }}>
          <Typography variant="h6">Status</Typography>
          {carregandoStatus && <CircularProgress size={16} />}
        </Stack>

        {statusErro && <Alert severity="warning" sx={{ mb: 2 }}>{statusErro}</Alert>}

        {status && (
          <>
            <Grid container spacing={2} sx={{ mb: 2 }}>
              <Grid item xs={6} sm={3}>
                <Typography variant="caption" color="text.secondary">Bateria</Typography>
                <Typography>
                  {status.bateria.tipo === 'Litio' && `${status.bateria.percentualLitio}%`}
                  {status.bateria.tipo === 'Chumbo' && `${status.bateria.tensaoChumboAproximada?.toFixed(1)}V (aprox.)`}
                  {status.bateria.tipo === 'SemBateria' && 'Sem bateria'}
                  {status.bateria.tipo === 'Carregando' && 'Carregando'}
                  {status.bateria.tipo === 'Reservado' && '—'}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="caption" color="text.secondary">Alimentação AC</Typography>
                <Chip
                  size="small"
                  label={status.problemas.alimentacaoAcNormal ? 'Normal' : 'Problema'}
                  color={status.problemas.alimentacaoAcNormal ? 'success' : 'error'}
                />
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="caption" color="text.secondary">Eletrificador</Typography>
                <Typography>{status.eletrificador.estado || 'Não programado'}</Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="caption" color="text.secondary">Data/Hora da Central</Typography>
                <Typography>{formatarDataHora(status.dataHoraCentral)}</Typography>
              </Grid>
            </Grid>

            <Typography variant="subtitle1" gutterBottom>Partições (16)</Typography>
            <Stack direction="row" flexWrap="wrap" gap={0.75} sx={{ mb: 2 }}>
              {status.particoes.map(p => (
                <Tooltip key={p.numero} title={p.desabilitada ? 'Não programada' : (p.estado ?? 'Desconhecido')}>
                  <Chip
                    size="small"
                    label={`P${p.numero}`}
                    color={p.estado ? COR_PARTICAO[p.estado] ?? 'default' : 'default'}
                    variant={p.desabilitada ? 'outlined' : 'filled'}
                  />
                </Tooltip>
              ))}
            </Stack>

            <ZonasPanel
              zonas={zonasVisiveis}
              totalZonas={status.zonas.length}
              centralId={centralId}
              onComandoConcluido={carregarStatus}
            />

            <Typography variant="subtitle1" gutterBottom>Problemas</Typography>
            <Stack direction="row" flexWrap="wrap" gap={0.75}>
              {Object.entries(status.problemas)
                .filter(([chave, valor]) => chave !== 'alimentacaoAcNormal' && valor === true)
                .map(([chave]) => (
                  <Chip key={chave} size="small" color="error" label={chave} />
                ))}
              {Object.entries(status.problemas).filter(([chave, valor]) => chave !== 'alimentacaoAcNormal' && valor === true).length === 0 && (
                <Typography variant="body2" color="text.secondary">Nenhum problema reportado.</Typography>
              )}
            </Stack>
          </>
        )}

        {!status && !statusErro && <Typography color="text.secondary">Consultando status…</Typography>}
      </Paper>

      <ArmPanel
        centralId={centralId}
        particoes={status?.particoes ?? []}
        eletrificador={status?.eletrificador}
        onComandoConcluido={carregarStatus}
      />

      <PgmPanel centralId={centralId} pgms={status?.pgms ?? []} onComandoConcluido={carregarStatus} />

      <DetalhesSessaoModal aberto={detalhesAbertos} sessao={sessao} onFechar={() => setDetalhesAbertos(false)} />

      <Dialog open={reconectarDialogAberto} onClose={() => setReconectarDialogAberto(false)}>
        <DialogTitle>Reconectar</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Isso <strong>não abre nenhuma conexão</strong> — apenas limpa a sessão registrada para
            esta central. A central deverá iniciar uma nova conexão automaticamente, conforme o
            protocolo (ela disca para o servidor, nunca o contrário).
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReconectarDialogAberto(false)} disabled={reconectando}>Cancelar</Button>
          <Button variant="contained" color="warning" onClick={confirmarReconectar} disabled={reconectando} autoFocus>
            {reconectando ? 'Processando...' : 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
