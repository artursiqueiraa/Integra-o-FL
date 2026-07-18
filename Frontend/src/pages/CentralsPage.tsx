import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import api from '../services/api'
import { Building, Central } from '../types'
import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, TextField, Button, Stack, IconButton, Typography, MenuItem, Select,
  InputLabel, FormControl, Alert, FormHelperText
} from '@mui/material'
import VisibilityIcon from '@mui/icons-material/Visibility'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'

export default function CentralsPage() {
  const [centrals, setCentrals] = useState<Central[]>([])
  const [buildings, setBuildings] = useState<Building[]>([])

  const [nome, setNome] = useState('')
  const [numeroSerie, setNumeroSerie] = useState('')
  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [editId, setEditId] = useState<number | null>(null)

  const [erro, setErro] = useState<string | null>(null)

  const carregar = async () => {
    const response = await api.get<Central[]>('/central')
    setCentrals(response.data)
  }

  const carregarBuildings = async () => {
    const response = await api.get<Building[]>('/building')
    setBuildings(response.data)
  }

  useEffect(() => {
    carregar()
    carregarBuildings()
  }, [])

  const limparForm = () => {
    setNome('')
    setNumeroSerie('')
    setBuildingId('')
    setEditId(null)
    setErro(null)
  }

  const salvar = async () => {
    if (!nome.trim() || !buildingId) {
      setErro('Preencha Nome e Prédio.')
      return
    }

    setErro(null)

    const payload = {
      id: editId || 0,
      nome,
      numeroSerie: numeroSerie.trim() || undefined,
      buildingId,
    }

    try {
      if (editId) {
        await api.put(`/central/${editId}`, payload)
      } else {
        await api.post('/central', payload)
      }
      limparForm()
      carregar()
    } catch {
      setErro('Não foi possível salvar a central.')
    }
  }

  const editar = (central: Central) => {
    setEditId(central.id)
    setNome(central.nome)
    setNumeroSerie(central.numeroSerie || '')
    setBuildingId(central.buildingId)
    setErro(null)
  }

  const excluir = async (id: number) => {
    setErro(null)
    try {
      await api.delete(`/central/${id}`)
      carregar()
    } catch {
      setErro('Não foi possível excluir a central.')
    }
  }

  return (
    <div>
      <Typography variant="h5" gutterBottom>Centrais</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}

      <Stack spacing={2} sx={{ mb: 3, maxWidth: 500 }}>
        <TextField label="Nome" value={nome} onChange={e => setNome(e.target.value)} />

        <FormControl>
          <TextField
            label="Número de Série"
            value={numeroSerie}
            onChange={e => setNumeroSerie(e.target.value)}
          />
          <FormHelperText>
            Necessário para a central conectar automaticamente ao servidor (a central disca para
            cá — nunca o contrário). Sem ele, a sessão fica sem vínculo com este cadastro.
          </FormHelperText>
        </FormControl>

        <FormControl>
          <InputLabel>Prédio</InputLabel>
          <Select
            label="Prédio"
            value={buildingId}
            onChange={e => setBuildingId(e.target.value as number)}
          >
            {buildings.map(b => (
              <MenuItem key={b.id} value={b.id}>{b.nome}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <Stack direction="row" spacing={2}>
          <Button variant="contained" onClick={salvar}>
            {editId ? 'Atualizar' : 'Salvar'}
          </Button>
          {editId && <Button onClick={limparForm}>Cancelar</Button>}
        </Stack>
      </Stack>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Id</TableCell>
              <TableCell>Nome</TableCell>
              <TableCell>Número de Série</TableCell>
              <TableCell>Fabricante</TableCell>
              <TableCell>Modelo</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Ações</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {centrals.map(c => (
              <TableRow key={c.id}>
                <TableCell>{c.id}</TableCell>
                <TableCell>{c.nome}</TableCell>
                <TableCell>{c.numeroSerie || '—'}</TableCell>
                <TableCell>{c.fabricante}</TableCell>
                <TableCell>{c.modelo}</TableCell>
                <TableCell>{c.status}</TableCell>
                <TableCell align="right">
                  <IconButton component={Link} to={`/centrais/${c.id}`} title="Ver central"><VisibilityIcon /></IconButton>
                  <IconButton onClick={() => editar(c)}><EditIcon /></IconButton>
                  <IconButton onClick={() => excluir(c.id)}><DeleteIcon /></IconButton>
                </TableCell>
              </TableRow>
            ))}
            {centrals.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">Nenhuma central cadastrada.</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </div>
  )
}
