import { useState } from 'react'
import api from '../services/api'
import { ZonaStatus, ZonaPredio } from '../types'
import {
  Typography, Stack, Chip, Tooltip, CircularProgress, Alert,
  Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button
} from '@mui/material'

interface Confirmacao {
  zona: number
  inibir: boolean
  rotulo: string
}

interface Props {
  zonas: ZonaStatus[]
  totalZonas: number
  centralId: number
  /**
   * Catálogo opcional (nome/tipo cadastrados via `ZonaPredioService`) — quando informado, o
   * painel mostra só as zonas cadastradas e ativas, com o nome do cadastro em vez de "Z-N"
   * genérico. Sem essa prop, comportamento idêntico ao de sempre (usado pela Tela Central).
   */
  catalogo?: ZonaPredio[]
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

export default function ZonasPanel({ zonas, totalZonas, centralId, catalogo, onComandoConcluido }: Props) {
  const [confirmacao, setConfirmacao] = useState<Confirmacao | null>(null)
  const [executandoZona, setExecutandoZona] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const zonasExibidas = catalogo
    ? zonas.filter(z => catalogo.some(item => item.numero === z.numero && item.ativa))
    : zonas

  const rotulo = (numero: number) => catalogo?.find(item => item.numero === numero)?.nome || `Z${numero}`

  const clicarZona = (zona: ZonaStatus) => {
    if (!zona.permiteInibir) {
      return
    }
    setErro(null)
    setConfirmacao({ zona: zona.numero, inibir: zona.estado !== 'Inibida', rotulo: rotulo(zona.numero) })
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
      setErro(resposta?.data?.mensagem || `Falha ao ${inibir ? 'inibir' : 'desinibir'} ${confirmacao.rotulo}.`)
    } finally {
      setExecutandoZona(null)
    }
  }

  return (
    <>
      <Typography variant="subtitle1" gutterBottom>
        Zonas ({zonasExibidas.length} de {totalZonas} ativas) — clique numa zona com permissão para inibir/desinibir
      </Typography>

      {erro && <Alert severity="error" sx={{ mb: 1 }} onClose={() => setErro(null)}>{erro}</Alert>}

      <Stack direction="row" flexWrap="wrap" gap={0.75} sx={{ mb: 2 }}>
        {zonasExibidas.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            {catalogo ? 'Nenhuma zona cadastrada para esta central.' : 'Nenhuma zona ativa reportada.'}
          </Typography>
        )}
        {zonasExibidas.map(z => (
          <Tooltip
            key={z.numero}
            title={z.permiteInibir ? `${z.estado ?? 'Desconhecido'} — clique para ${z.estado === 'Inibida' ? 'desinibir' : 'inibir'}` : (z.estado ?? 'Desconhecido')}
          >
            <Chip
              size="small"
              label={executandoZona === z.numero ? <CircularProgress size={12} /> : rotulo(z.numero)}
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
            {confirmacao && `${confirmacao.inibir ? 'Inibir' : 'Desinibir'} ${confirmacao.rotulo}?`}
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
