import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Grid, Typography } from '@mui/material'
import { SessaoInfo } from '../../types'
import { formatarDataHora, formatarDuracaoDesde } from '../../utils/formatters'

interface Props {
  aberto: boolean
  sessao: SessaoInfo | null
  onFechar: () => void
}

function Campo({ titulo, valor }: { titulo: string; valor: string }) {
  return (
    <Grid item xs={6} sm={4}>
      <Typography variant="caption" color="text.secondary">{titulo}</Typography>
      <Typography>{valor}</Typography>
    </Grid>
  )
}

export default function DetalhesSessaoModal({ aberto, sessao, onFechar }: Props) {
  return (
    <Dialog open={aberto} onClose={onFechar} maxWidth="sm" fullWidth>
      <DialogTitle>Detalhes da Sessão</DialogTitle>
      <DialogContent>
        {sessao && (
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Campo titulo="Número Série" valor={sessao.numeroSerie || '—'} />
            <Campo titulo="Modelo" valor={sessao.modelo || '—'} />
            <Campo titulo="Firmware" valor={sessao.firmware || '—'} />
            <Campo titulo="MAC" valor={sessao.mac || '—'} />
            <Campo titulo="IP" valor={sessao.ipSessao || '—'} />
            <Campo titulo="Porta" valor={sessao.portaRemota?.toString() || '—'} />
            <Campo titulo="Hora da Conexão" valor={formatarDataHora(sessao.dataHoraConexaoUtc)} />
            <Campo titulo="Hora do Último KeepAlive" valor={formatarDataHora(sessao.ultimoKeepAliveEmUtc)} />
            <Campo
              titulo="Tempo Conectado"
              valor={sessao.sessaoAtiva ? formatarDuracaoDesde(sessao.dataHoraConexaoUtc) : '—'}
            />
            <Campo titulo="Último Comando" valor={sessao.ultimoComando || '—'} />
            <Campo titulo="Último Pacote" valor={formatarDataHora(sessao.ultimoPacoteRecebidoEmUtc)} />
            <Campo titulo="Bytes Recebidos" valor={sessao.bytesRecebidos?.toString() || '—'} />
            <Campo titulo="Bytes Enviados" valor={sessao.bytesEnviados?.toString() || '—'} />
            <Campo
              titulo="SEQ"
              valor={sessao.ultimoSeq != null ? `0x${sessao.ultimoSeq.toString(16).toUpperCase().padStart(2, '0')}` : '—'}
            />
          </Grid>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onFechar}>Fechar</Button>
      </DialogActions>
    </Dialog>
  )
}
