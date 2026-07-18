import { useState } from 'react'
import api from '../services/api'
import { ZonaStatus } from '../types'
import {
  Typography, Stack, Chip, Tooltip, CircularProgress, Alert,
  Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button
} from '@mui/material'

interface Confirmacao {
  zona: number
  inibir: boolean
}

interface Props {
  zonas: ZonaStatus[]
  totalZonas: number
  centralId: number
  /** Chamado apos qualquer comando concluir com sucesso, para o chamador atualizar o status. */
  onComandoConcluido: () => void
}

const COR_ZONA: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Fechada: 'success',
  Aberta: 'warning',
  Disparo: 'error',
  Inibida: 'default',
  Curto: 'error',
  TamperAberto: 'error',
  SemComunicacao: 'default',
  BateriaBaixa: 'warning',
  Desabilitada: 'default',
}

export default function ZonasPanel({ zonas, totalZonas, centralId, onComandoConcluido }: Props) {
  const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)
  const [executandoZona, setExecutandoZona] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const clicarZona = (zona: ZonaStatus) => {
    if (!zona.permiteInibir) {
      return
    }
    setErro(null)
    setConfirmacao({ zona: zona.numero, inibir: zona.estado !== 'Inibida' })
  }

  const executar = async () => {
    if (!confirmacao) return
    const { zona, inibir } = confirmacao
    setConfirmacao(null)
    setExecutandoZona(zona)
    setErro(null)

    try {
      await api.post(`/centrais/${centralId}/zonas/${zona}/${inibir ? 'inibir' : 'desinibir'}`)
      onComandoConcluido()
    } catch (e) {
      const resposta = (e as { response?: { data?: { mensagem?: string } } }).response
      setErro(resposta?.data?.mensagem || `Falha ao ${inibir ? 'inibir' : 'desinibir'} a zona ${zona}.`)
    } finally {
      setExecutandoZona(null)
    }
  }

  return (
    <>
      <Typography variant="subtitle1" gutterBottom>
        Zonas ({zonas.length} de {totalZonas} ativas) — clique numa zona com permissão para inibir/desinibir
      </Typography>

      {erro && <Alert severity="error" sx={{ mb: 1 }} onClose={() => setErro(null)}>{erro}</Alert>}

      <Stack direction="row" flexWrap="wrap" gap={0.75} sx={{ mb: 2 }}>
        {zonas.length === 0 && (
          <Typography variant="body2" color="text.secondary">Nenhuma zona ativa reportada.</Typography>
        )}
        {zonas.map(z => (
          <Tooltip
            key={z.numero}
            title={z.permiteInibir ? `${z.estado ?? 'Desconhecido'} — clique para ${z.estado === 'Inibida' ? 'desinibir' : 'inibir'}` : (z.estado ?? 'Desconhecido')}
          >
            <Chip
              size="small"
              label={executandoZona === z.numero ? <CircularProgress size={12} /> : `Z${z.numero}`}
              color={z.estado ? COR_ZONA[z.estado] ?? 'default' : 'default'}
              variant={z.estado === 'Inibida' ? 'outlined' : 'filled'}
              onClick={z.permiteInibir ? () => clicarZona(z) : undefined}
              sx={{ cursor: z.permiteInibir ? 'pointer' : 'default' }}
            />
          </Tooltip>
        ))}
      </Stack>

      <Dialog open={confirmacao !== null} onClose={() => setConfirmacao(null)}>
        <DialogTitle>Confirmar comando</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirmacao && `${confirmacao.inibir ? 'Inibir' : 'Desinibir'} a zona ${confirmacao.zona}?`}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmacao(null)}>Cancelar</Button>
          <Button variant="contained" onClick={executar} autoFocus>Confirmar</Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
