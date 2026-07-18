import { Paper, Typography, List, ListItem, ListItemIcon, ListItemText } from '@mui/material'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import CancelIcon from '@mui/icons-material/Cancel'
import HelpOutlineIcon from '@mui/icons-material/HelpOutline'
import { DiagnosticoResultado } from '../../types'

interface Props {
  diagnostico: DiagnosticoResultado | null
}

export default function DiagnosticoPanel({ diagnostico }: Props) {
  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>Diagnóstico</Typography>

      {!diagnostico && <Typography color="text.secondary">Consultando diagnóstico…</Typography>}

      {diagnostico && (
        <List dense disablePadding>
          {diagnostico.itens.map((item, indice) => (
            <ListItem key={indice} disableGutters>
              <ListItemIcon sx={{ minWidth: 32 }}>
                {item.ok === true && <CheckCircleIcon color="success" fontSize="small" />}
                {item.ok === false && <CancelIcon color="error" fontSize="small" />}
                {item.ok === null || item.ok === undefined ? <HelpOutlineIcon color="disabled" fontSize="small" /> : null}
              </ListItemIcon>
              <ListItemText primary={item.descricao} secondary={item.detalhe} />
            </ListItem>
          ))}
        </List>
      )}
    </Paper>
  )
}
