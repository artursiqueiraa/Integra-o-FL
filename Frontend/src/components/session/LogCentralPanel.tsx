import { useEffect, useState } from 'react'
import { Paper, Typography, List, ListItem, ListItemText, Box } from '@mui/material'
import api from '../../services/api'
import { AtividadeLogEntry } from '../../types'

const INTERVALO_LOG_MS = 4000

interface Props {
  centralId: number
}

function formatarHora(iso: string): string {
  return new Date(iso).toLocaleTimeString()
}

export default function LogCentralPanel({ centralId }: Props) {
  const [entradas, setEntradas] = useState<AtividadeLogEntry[]>([])

  useEffect(() => {
    let cancelado = false

    const carregar = async () => {
      try {
        const response = await api.get<AtividadeLogEntry[]>(`/centrais/${centralId}/log`, { params: { max: 100 } })
        if (!cancelado) {
          setEntradas(response.data)
        }
      } catch {
        // painel de log é best-effort: uma falha pontual não deve poluir a tela com alertas
      }
    }

    carregar()
    const id = setInterval(carregar, INTERVALO_LOG_MS)
    return () => {
      cancelado = true
      clearInterval(id)
    }
  }, [centralId])

  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>Log da Central</Typography>
      <Box sx={{ maxHeight: 320, overflowY: 'auto' }}>
        <List dense disablePadding>
          {entradas.map((entrada, indice) => (
            <ListItem key={indice} disableGutters divider>
              <ListItemText
                primary={entrada.mensagem}
                secondary={formatarHora(entrada.timestamp)}
                primaryTypographyProps={{ variant: 'body2', fontFamily: 'monospace' }}
                secondaryTypographyProps={{ variant: 'caption' }}
              />
            </ListItem>
          ))}
          {entradas.length === 0 && (
            <Typography variant="body2" color="text.secondary">Nenhuma atividade registrada ainda.</Typography>
          )}
        </List>
      </Box>
    </Paper>
  )
}
