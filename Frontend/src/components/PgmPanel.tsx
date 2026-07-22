import { useState } from 'react'
import api from '../services/api'
import { PgmStatus, PgmPredio } from '../types'
import {
  Paper, Typography, Grid, Card, CardContent, CardActions, Button, Chip,
  Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions,
  TextField, CircularProgress, Alert, Stack
} from '@mui/material'
import PowerSettingsNewIcon from '@mui/icons-material/PowerSettingsNew'
import PowerOffIcon from '@mui/icons-material/PowerOff'
import BoltIcon from '@mui/icons-material/Bolt'

type Acao = 'ligar' | 'desligar' | 'pulso'

interface Confirmacao {
  pgm: number
  acao: Acao
  rotulo: string
}

interface Props {
  centralId: number
  pgms: PgmStatus[]
  /**
   * Catálogo opcional (nome/tipo cadastrados via `PgmPredioService`) — quando informado, o
   * painel mostra só as PGMs cadastradas e ativas, com o nome do cadastro em vez de "PGM N"
   * genérico. Sem essa prop, comportamento idêntico ao de sempre (16 PGMs por número, usado
   * pela Tela Central). O envio do comando é sempre o mesmo endpoint real, com ou sem catálogo.
   */
  catalogo?: PgmPredio[]
  /** Chamado apos qualquer comando concluir com sucesso, para o chamador atualizar o status. */
  onComandoConcluido: () => void
}

const ROTULO_ACAO: Record<Acao, string> = {
  ligar: 'Ligar',
  desligar: 'Desligar',
  pulso: 'Pulso',
}

export default function PgmPanel({ centralId, pgms, catalogo, onComandoConcluido }: Props) {
  const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)
  const [duracaoPulsoMs, setDuracaoPulsoMs] = useState(1000)
  const [executandoPgm, setExecutandoPgm] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)

  const numeros = catalogo
    ? catalogo.filter(item => item.ativa).map(item => item.numero).sort((a, b) => a - b)
    : Array.from({ length: 16 }, (_, i) => i + 1)

  const rotulo = (numero: number) => catalogo?.find(item => item.numero === numero)?.nome || `PGM ${numero}`
  const tipo = (numero: number) => catalogo?.find(item => item.numero === numero)?.tipo

  const pedirConfirmacao = (numero: number, acao: Acao) => {
    setErro(null)
    setSucesso(null)
    setConfirmacao({ pgm: numero, acao, rotulo: rotulo(numero) })
  }

  const executar = async () => {
    if (!confirmacao) return
    const { pgm, acao } = confirmacao
    setConfirmacao(null)
    setExecutandoPgm(pgm)
    setErro(null)
    setSucesso(null)

    try {
      const path = `/centrais/${centralId}/pgm/${pgm}/${acao}`
      const body = acao === 'pulso' ? { duracaoMs: duracaoPulsoMs } : undefined
      await api.post(path, body)
      setSucesso(`${confirmacao.rotulo}: comando "${ROTULO_ACAO[acao]}" executado com sucesso.`)
      onComandoConcluido()
    } catch (e) {
      const resposta = (e as { response?: { data?: { mensagem?: string } } }).response
      setErro(resposta?.data?.mensagem || `Falha ao executar "${ROTULO_ACAO[acao]}" em ${confirmacao.rotulo}.`)
    } finally {
      setExecutandoPgm(null)
    }
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>PGMs</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}
      {sucesso && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSucesso(null)}>{sucesso}</Alert>}

      {numeros.length === 0 && (
        <Typography variant="body2" color="text.secondary">Nenhuma PGM cadastrada para esta central.</Typography>
      )}

      <Grid container spacing={1.5}>
        {numeros.map(numero => {
          const pgm = pgms.find(p => p.numero === numero)
          const acionada = pgm?.acionada ?? false
          const permitida = pgm?.permitida ?? false
          const executando = executandoPgm === numero

          return (
            <Grid item xs={12} sm={6} md={4} lg={3} key={numero}>
              <Card variant="outlined">
                <CardContent sx={{ pb: 1 }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography variant="subtitle2">{rotulo(numero)}</Typography>
                    {executando
                      ? <CircularProgress size={16} />
                      : <Chip
                          size="small"
                          label={acionada ? 'Ligada' : 'Desligada'}
                          color={acionada ? 'success' : 'default'}
                        />
                    }
                  </Stack>
                  <Typography variant="caption" color="text.secondary">
                    {catalogo ? `PGM ${numero}${tipo(numero) ? ` · ${tipo(numero)}` : ''}` : null}
                  </Typography>
                  {!permitida && (
                    <Typography variant="caption" color="text.secondary" display="block">Sem permissão / não configurada</Typography>
                  )}
                </CardContent>
                <CardActions sx={{ pt: 0, gap: 0.5 }}>
                  <Button
                    size="small" color="success" startIcon={<PowerSettingsNewIcon />}
                    disabled={executando}
                    onClick={() => pedirConfirmacao(numero, 'ligar')}
                  >
                    Ligar
                  </Button>
                  <Button
                    size="small" color="error" startIcon={<PowerOffIcon />}
                    disabled={executando}
                    onClick={() => pedirConfirmacao(numero, 'desligar')}
                  >
                    Desligar
                  </Button>
                  <Button
                    size="small" startIcon={<BoltIcon />}
                    disabled={executando}
                    onClick={() => pedirConfirmacao(numero, 'pulso')}
                  >
                    Pulso
                  </Button>
                </CardActions>
              </Card>
            </Grid>
          )
        })}
      </Grid>

      <Dialog open={confirmacao !== null} onClose={() => setConfirmacao(null)}>
        <DialogTitle>Confirmar comando</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirmacao && `Enviar o comando "${ROTULO_ACAO[confirmacao.acao]}" para ${confirmacao.rotulo}?`}
          </DialogContentText>
          {confirmacao?.acao === 'pulso' && (
            <TextField
              sx={{ mt: 2 }}
              label="Duração do pulso (ms)"
              type="number"
              fullWidth
              value={duracaoPulsoMs}
              onChange={e => setDuracaoPulsoMs(Number(e.target.value))}
              inputProps={{ min: 100, max: 60000 }}
              helperText="Entre 100 e 60000 ms"
            />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmacao(null)}>Cancelar</Button>
          <Button variant="contained" onClick={executar} autoFocus>Confirmar</Button>
        </DialogActions>
      </Dialog>
    </Paper>
  )
}
