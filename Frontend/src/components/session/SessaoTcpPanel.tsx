import { Paper, Typography, Grid } from '@mui/material'
import { SessaoInfo } from '../../types'
import { formatarDataHora, formatarDuracaoDesde } from '../../utils/formatters'

interface Props {
  sessao: SessaoInfo
}

function Campo({ titulo, valor }: { titulo: string; valor: string }) {
  return (
    <Grid item xs={6} sm={4} md={3}>
      <Typography variant="caption" color="text.secondary">{titulo}</Typography>
      <Typography>{valor}</Typography>
    </Grid>
  )
}

export default function SessaoTcpPanel({ sessao }: Props) {
  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>Sessão TCP</Typography>
      <Grid container spacing={2}>
        <Campo titulo="IP Remoto" valor={sessao.ipSessao || '—'} />
        <Campo titulo="Porta Remota" valor={sessao.portaRemota?.toString() || '—'} />
        <Campo titulo="Socket Conectado" valor={sessao.socketConectado ? 'Sim' : 'Não'} />
        <Campo titulo="Handshake Realizado" valor={sessao.handshakeRealizado ? 'Sim' : 'Não'} />
        <Campo titulo="KeepAlive Ativo" valor={sessao.keepAliveAtivo ? 'Sim' : 'Não'} />
        <Campo
          titulo="Tempo Conectado"
          valor={sessao.sessaoAtiva ? formatarDuracaoDesde(sessao.dataHoraConexaoUtc) : '—'}
        />
        <Campo titulo="Último Pacote Recebido" valor={formatarDataHora(sessao.ultimoPacoteRecebidoEmUtc)} />
        <Campo titulo="Último Comando Recebido" valor={sessao.ultimoComando || '—'} />
        <Campo titulo="SEQ Atual" valor={sessao.ultimoSeq != null ? `0x${sessao.ultimoSeq.toString(16).toUpperCase().padStart(2, '0')}` : '—'} />
        <Campo titulo="Último Erro" valor={sessao.ultimoErro || '—'} />
      </Grid>
    </Paper>
  )
}
