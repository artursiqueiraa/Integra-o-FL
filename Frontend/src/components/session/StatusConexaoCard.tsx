import { Paper, Typography, Grid, Chip, Stack, Button, CircularProgress } from '@mui/material'
import RefreshIcon from '@mui/icons-material/Refresh'
import { SessaoInfo } from '../../types'
import { formatarDataHora, formatarDuracaoDesde } from '../../utils/formatters'
import IndicadoresChips from './IndicadoresChips'

interface Props {
  sessao: SessaoInfo | null
  carregando: boolean
  aguardandoConexao: boolean
  onAtualizar: () => void
}

export default function StatusConexaoCard({ sessao, carregando, aguardandoConexao, onAtualizar }: Props) {
  const statusExibido = aguardandoConexao ? 'Aguardando conexão' : sessao?.statusConexao ?? 'Offline'
  const emoji = statusExibido === 'Online' ? '🟢' : statusExibido === 'Aguardando conexão' ? '🟡' : '🔴'
  const cor = statusExibido === 'Online' ? 'success' : statusExibido === 'Aguardando conexão' ? 'warning' : 'error'

  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Stack direction="row" alignItems="center" spacing={1.5}>
          <Typography variant="h6">Status da Conexão</Typography>
          <Chip label={`${emoji} ${statusExibido}`} color={cor} variant="outlined" />
          {carregando && <CircularProgress size={16} />}
        </Stack>
        <Button variant="outlined" size="small" startIcon={<RefreshIcon />} onClick={onAtualizar}>
          Atualizar Status
        </Button>
      </Stack>

      {sessao && (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Número de Série</Typography>
              <Typography>{sessao.numeroSerie || '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Modelo</Typography>
              <Typography>{sessao.modelo || '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Firmware</Typography>
              <Typography>{sessao.firmware || '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">IP da Sessão</Typography>
              <Typography>{sessao.ipSessao || '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Porta Remota</Typography>
              <Typography>{sessao.portaRemota ?? '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Data/Hora da Conexão</Typography>
              <Typography>{formatarDataHora(sessao.dataHoraConexaoUtc)}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Último KeepAlive</Typography>
              <Typography>{formatarDataHora(sessao.ultimoKeepAliveEmUtc)}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Tempo Conectado</Typography>
              <Typography>
                {sessao.sessaoAtiva ? formatarDuracaoDesde(sessao.dataHoraConexaoUtc) : '—'}
              </Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Latência</Typography>
              <Typography>{sessao.latenciaMs != null ? `${sessao.latenciaMs.toFixed(0)}ms` : '—'}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Tempo desde o Último KeepAlive</Typography>
              <Typography>{formatarDuracaoDesde(sessao.ultimoKeepAliveEmUtc)}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">Sessão Ativa</Typography>
              <Typography>{sessao.sessaoAtiva ? 'Sim' : 'Não'}</Typography>
            </Grid>
          </Grid>

          <IndicadoresChips sessao={sessao} />
        </>
      )}

      {!sessao && !carregando && (
        <Typography color="text.secondary">Não foi possível consultar a sessão.</Typography>
      )}
    </Paper>
  )
}
