import { useState } from 'react'
import api from '../services/api'
import { ParticaoStatus, Eletrificador } from '../types'
import {
  Paper, Typography, Grid, Card, CardContent, CardActions, Button, Chip,
  Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions,
  CircularProgress, Alert, Stack
} from '@mui/material'
import LockIcon from '@mui/icons-material/Lock'
import LockOpenIcon from '@mui/icons-material/LockOpen'
import HomeIcon from '@mui/icons-material/Home'
import DirectionsRunIcon from '@mui/icons-material/DirectionsRun'

type Acao = 'armar' | 'desarmar' | 'armar-stay' | 'armar-away'

interface Confirmacao {
  particao: number
  acao: Acao
  rotulo: string
}

interface Props {
  centralId: number
  particoes: ParticaoStatus[]
  eletrificador?: Eletrificador
  /** Chamado apos qualquer comando concluir com sucesso, para o chamador atualizar o status. */
  onComandoConcluido: () => void
}

const ROTULO_ACAO: Record<Acao, string> = {
  armar: 'Armar',
  desarmar: 'Desarmar',
  'armar-stay': 'Armar Stay',
  'armar-away': 'Armar Away',
}

const COR_ESTADO: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Desarmada: 'default',
  Armada: 'success',
  ArmadaStay: 'success',
  DesarmadaEmDisparo: 'error',
  ArmadaEmDisparo: 'error',
  ArmadaStayEmDisparo: 'error',
  Desarmado: 'default',
  Armado: 'success',
  DesarmadoEmDisparo: 'error',
  ArmadoEmDisparo: 'error',
}

/** Numero especial que opera o eletrificador (nao e uma particao de verdade — ver Documentation/Protocol/10_ARM.md). */
const PARTICAO_ELETRIFICADOR = 99

export default function ArmPanel({ centralId, particoes, eletrificador, onComandoConcluido }: Props) {
  const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)
  const [executandoParticao, setExecutandoParticao] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)

  const pedirConfirmacao = (particao: number, acao: Acao) => {
    setErro(null)
    setSucesso(null)
    setConfirmacao({ particao, acao, rotulo: particao === PARTICAO_ELETRIFICADOR ? 'Eletrificador' : `Partição ${particao}` })
  }

  const executar = async () => {
    if (!confirmacao) return
    const { particao, acao } = confirmacao
    setConfirmacao(null)
    setExecutandoParticao(particao)
    setErro(null)
    setSucesso(null)

    try {
      await api.post(`/centrais/${centralId}/particoes/${particao}/${acao}`)
      setSucesso(`${confirmacao.rotulo}: comando "${ROTULO_ACAO[acao]}" executado com sucesso.`)
      onComandoConcluido()
    } catch (e) {
      const resposta = (e as { response?: { data?: { mensagem?: string } } }).response
      setErro(resposta?.data?.mensagem || `Falha ao executar "${ROTULO_ACAO[acao]}" em ${confirmacao.rotulo}.`)
    } finally {
      setExecutandoParticao(null)
    }
  }

  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>Arme (Partições)</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}
      {sucesso && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSucesso(null)}>{sucesso}</Alert>}

      <Grid container spacing={1.5}>
        {Array.from({ length: 16 }, (_, i) => i + 1).map(numero => {
          const particao = particoes.find(p => p.numero === numero)
          const executando = executandoParticao === numero

          if (particao?.desabilitada) {
            return null
          }

          return (
            <Grid item xs={12} sm={6} md={4} lg={3} key={numero}>
              <Card variant="outlined">
                <CardContent sx={{ pb: 1 }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography variant="subtitle2">Partição {numero}</Typography>
                    {executando
                      ? <CircularProgress size={16} />
                      : <Chip
                          size="small"
                          label={particao?.estado ?? 'Desconhecido'}
                          color={particao?.estado ? COR_ESTADO[particao.estado] ?? 'default' : 'default'}
                        />
                    }
                  </Stack>
                </CardContent>
                <CardActions sx={{ pt: 0, gap: 0.5, flexWrap: 'wrap' }}>
                  <Button
                    size="small" color="success" startIcon={<LockIcon />}
                    disabled={executando || (particao ? !particao.permiteArmar : false)}
                    onClick={() => pedirConfirmacao(numero, 'armar')}
                  >
                    Armar
                  </Button>
                  <Button
                    size="small" color="error" startIcon={<LockOpenIcon />}
                    disabled={executando || (particao ? !particao.permiteDesarmar : false)}
                    onClick={() => pedirConfirmacao(numero, 'desarmar')}
                  >
                    Desarmar
                  </Button>
                  <Button
                    size="small" startIcon={<HomeIcon />}
                    disabled={executando || (particao ? !particao.permiteArmarStay : false)}
                    onClick={() => pedirConfirmacao(numero, 'armar-stay')}
                  >
                    Stay
                  </Button>
                  <Button
                    size="small" startIcon={<DirectionsRunIcon />}
                    disabled={executando || (particao ? !particao.permiteArmarAway : false)}
                    onClick={() => pedirConfirmacao(numero, 'armar-away')}
                  >
                    Away
                  </Button>
                </CardActions>
              </Card>
            </Grid>
          )
        })}

        <Grid item xs={12} sm={6} md={4} lg={3}>
          <Card variant="outlined">
            <CardContent sx={{ pb: 1 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography variant="subtitle2">Eletrificador</Typography>
                {executandoParticao === PARTICAO_ELETRIFICADOR
                  ? <CircularProgress size={16} />
                  : <Chip
                      size="small"
                      label={eletrificador?.estado ?? 'Desconhecido'}
                      color={eletrificador?.estado ? COR_ESTADO[eletrificador.estado] ?? 'default' : 'default'}
                    />
                }
              </Stack>
              <Typography variant="caption" color="text.secondary">Comando especial (partição 99)</Typography>
            </CardContent>
            <CardActions sx={{ pt: 0, gap: 0.5, flexWrap: 'wrap' }}>
              <Button
                size="small" color="success" startIcon={<LockIcon />}
                disabled={executandoParticao === PARTICAO_ELETRIFICADOR}
                onClick={() => pedirConfirmacao(PARTICAO_ELETRIFICADOR, 'armar')}
              >
                Armar
              </Button>
              <Button
                size="small" color="error" startIcon={<LockOpenIcon />}
                disabled={executandoParticao === PARTICAO_ELETRIFICADOR || (eletrificador ? !eletrificador.permiteDesarmar : false)}
                onClick={() => pedirConfirmacao(PARTICAO_ELETRIFICADOR, 'desarmar')}
              >
                Desarmar
              </Button>
              <Button
                size="small" startIcon={<DirectionsRunIcon />}
                disabled={executandoParticao === PARTICAO_ELETRIFICADOR || (eletrificador ? !eletrificador.permiteArmarAway : false)}
                onClick={() => pedirConfirmacao(PARTICAO_ELETRIFICADOR, 'armar-away')}
              >
                Away
              </Button>
            </CardActions>
          </Card>
        </Grid>
      </Grid>

      <Dialog open={confirmacao !== null} onClose={() => setConfirmacao(null)}>
        <DialogTitle>Confirmar comando</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirmacao && `Enviar o comando "${ROTULO_ACAO[confirmacao.acao]}" para ${confirmacao.rotulo}?`}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmacao(null)}>Cancelar</Button>
          <Button variant="contained" onClick={executar} autoFocus>Confirmar</Button>
        </DialogActions>
      </Dialog>
    </Paper>
  )
}
