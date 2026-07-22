import { useEffect, useState } from 'react'
import api from '../services/api'
import { PgmPredio, ZonaPredio } from '../types'
import {
  Accordion, AccordionSummary, AccordionDetails, Typography, Grid, TextField, Button,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, IconButton,
  Stack, MenuItem, Select, InputLabel, FormControl, FormControlLabel, Switch, Alert
} from '@mui/material'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'

const TIPOS_PGM = ['Portão', 'Luz', 'Sirene', 'Fechadura', 'Genérico']
const ICONES_PGM = [
  { valor: 'portao', rotulo: 'Portão' },
  { valor: 'luz', rotulo: 'Luz' },
  { valor: 'sirene', rotulo: 'Sirene' },
  { valor: 'fechadura', rotulo: 'Fechadura' },
  { valor: 'generico', rotulo: 'Genérico' },
]
const TIPOS_ZONA = ['Porta', 'Janela', 'Movimento', 'Perimetral', 'Genérica']

interface Props {
  buildingId: number
  centralId: number
  /** Chamado sempre que uma PGM ou Zona é criada/atualizada/excluída, para o painel de Operação recarregar os nomes. */
  onCatalogoAlterado?: () => void
}

export default function CadastroPgmZonaPanel({ buildingId, centralId, onCatalogoAlterado }: Props) {
  const [pgms, setPgms] = useState<PgmPredio[]>([])
  const [zonas, setZonas] = useState<ZonaPredio[]>([])
  const [erro, setErro] = useState<string | null>(null)

  const [pgmEditId, setPgmEditId] = useState<number | null>(null)
  const [pgmNumero, setPgmNumero] = useState(1)
  const [pgmNome, setPgmNome] = useState('')
  const [pgmTipo, setPgmTipo] = useState(TIPOS_PGM[0])
  const [pgmIcone, setPgmIcone] = useState(ICONES_PGM[0].valor)
  const [pgmAtiva, setPgmAtiva] = useState(true)

  const [zonaEditId, setZonaEditId] = useState<number | null>(null)
  const [zonaNumero, setZonaNumero] = useState(1)
  const [zonaNome, setZonaNome] = useState('')
  const [zonaTipo, setZonaTipo] = useState(TIPOS_ZONA[0])
  const [zonaAtiva, setZonaAtiva] = useState(true)

  const carregarPgms = async () => {
    const response = await api.get<PgmPredio[]>('/pgmpredio', { params: { buildingId, centralId } })
    setPgms(response.data)
  }

  const carregarZonas = async () => {
    const response = await api.get<ZonaPredio[]>('/zonapredio', { params: { buildingId, centralId } })
    setZonas(response.data)
  }

  useEffect(() => {
    carregarPgms()
    carregarZonas()
  }, [buildingId, centralId])

  const limparFormPgm = () => {
    setPgmEditId(null)
    setPgmNumero(1)
    setPgmNome('')
    setPgmTipo(TIPOS_PGM[0])
    setPgmIcone(ICONES_PGM[0].valor)
    setPgmAtiva(true)
  }

  const salvarPgm = async () => {
    if (!pgmNome.trim()) {
      setErro('Informe o nome da PGM.')
      return
    }
    setErro(null)
    try {
      if (pgmEditId) {
        await api.put(`/pgmpredio/${pgmEditId}`, { nome: pgmNome, tipo: pgmTipo, icone: pgmIcone, ativa: pgmAtiva })
      } else {
        await api.post('/pgmpredio', { buildingId, centralId, numero: pgmNumero, nome: pgmNome, tipo: pgmTipo, icone: pgmIcone, ativa: pgmAtiva })
      }
      limparFormPgm()
      carregarPgms()
      onCatalogoAlterado?.()
    } catch (e) {
      const resposta = (e as { response?: { data?: string | { mensagem?: string } } }).response
      setErro(typeof resposta?.data === 'string' ? resposta.data : resposta?.data?.mensagem || 'Não foi possível salvar a PGM.')
    }
  }

  const editarPgm = (pgm: PgmPredio) => {
    setPgmEditId(pgm.id)
    setPgmNumero(pgm.numero)
    setPgmNome(pgm.nome)
    setPgmTipo(pgm.tipo || TIPOS_PGM[0])
    setPgmIcone(pgm.icone || ICONES_PGM[0].valor)
    setPgmAtiva(pgm.ativa)
  }

  const excluirPgm = async (id: number) => {
    try {
      await api.delete(`/pgmpredio/${id}`)
      carregarPgms()
      onCatalogoAlterado?.()
    } catch {
      setErro('Não foi possível excluir a PGM.')
    }
  }

  const limparFormZona = () => {
    setZonaEditId(null)
    setZonaNumero(1)
    setZonaNome('')
    setZonaTipo(TIPOS_ZONA[0])
    setZonaAtiva(true)
  }

  const salvarZona = async () => {
    if (!zonaNome.trim()) {
      setErro('Informe o nome da zona.')
      return
    }
    setErro(null)
    try {
      if (zonaEditId) {
        await api.put(`/zonapredio/${zonaEditId}`, { nome: zonaNome, tipo: zonaTipo, ativa: zonaAtiva })
      } else {
        await api.post('/zonapredio', { buildingId, centralId, numero: zonaNumero, nome: zonaNome, tipo: zonaTipo, ativa: zonaAtiva })
      }
      limparFormZona()
      carregarZonas()
      onCatalogoAlterado?.()
    } catch (e) {
      const resposta = (e as { response?: { data?: string | { mensagem?: string } } }).response
      setErro(typeof resposta?.data === 'string' ? resposta.data : resposta?.data?.mensagem || 'Não foi possível salvar a zona.')
    }
  }

  const editarZona = (zona: ZonaPredio) => {
    setZonaEditId(zona.id)
    setZonaNumero(zona.numero)
    setZonaNome(zona.nome)
    setZonaTipo(zona.tipo || TIPOS_ZONA[0])
    setZonaAtiva(zona.ativa)
  }

  const excluirZona = async (id: number) => {
    try {
      await api.delete(`/zonapredio/${id}`)
      carregarZonas()
      onCatalogoAlterado?.()
    } catch {
      setErro('Não foi possível excluir a zona.')
    }
  }

  return (
    <Accordion sx={{ mb: 3 }}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography>Gerenciar Cadastro de PGMs e Zonas</Typography>
      </AccordionSummary>
      <AccordionDetails>
        {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}

        <Grid container spacing={3}>
          <Grid item xs={12} md={6}>
            <Typography variant="subtitle1" gutterBottom>PGMs</Typography>
            <Stack spacing={1.5} sx={{ mb: 2 }}>
              <Stack direction="row" spacing={1.5}>
                <TextField
                  label="Número (1-16)" type="number" size="small" sx={{ width: 130 }}
                  value={pgmNumero} onChange={e => setPgmNumero(Number(e.target.value))}
                  disabled={pgmEditId !== null}
                  inputProps={{ min: 1, max: 16 }}
                />
                <TextField label="Nome" size="small" fullWidth value={pgmNome} onChange={e => setPgmNome(e.target.value)} />
              </Stack>
              <Stack direction="row" spacing={1.5}>
                <FormControl size="small" fullWidth>
                  <InputLabel>Tipo</InputLabel>
                  <Select label="Tipo" value={pgmTipo} onChange={e => setPgmTipo(e.target.value)}>
                    {TIPOS_PGM.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
                  </Select>
                </FormControl>
                <FormControl size="small" fullWidth>
                  <InputLabel>Ícone</InputLabel>
                  <Select label="Ícone" value={pgmIcone} onChange={e => setPgmIcone(e.target.value)}>
                    {ICONES_PGM.map(i => <MenuItem key={i.valor} value={i.valor}>{i.rotulo}</MenuItem>)}
                  </Select>
                </FormControl>
              </Stack>
              <FormControlLabel control={<Switch checked={pgmAtiva} onChange={e => setPgmAtiva(e.target.checked)} />} label="Ativa" />
              <Stack direction="row" spacing={1}>
                <Button variant="contained" size="small" onClick={salvarPgm}>{pgmEditId ? 'Atualizar' : 'Adicionar'}</Button>
                {pgmEditId && <Button size="small" onClick={limparFormPgm}>Cancelar</Button>}
              </Stack>
            </Stack>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Nº</TableCell><TableCell>Nome</TableCell><TableCell>Tipo</TableCell>
                    <TableCell>Ativa</TableCell><TableCell align="right">Ações</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {pgms.map(p => (
                    <TableRow key={p.id}>
                      <TableCell>{p.numero}</TableCell>
                      <TableCell>{p.nome}</TableCell>
                      <TableCell>{p.tipo}</TableCell>
                      <TableCell>{p.ativa ? 'Sim' : 'Não'}</TableCell>
                      <TableCell align="right">
                        <IconButton size="small" onClick={() => editarPgm(p)}><EditIcon fontSize="small" /></IconButton>
                        <IconButton size="small" onClick={() => excluirPgm(p.id)}><DeleteIcon fontSize="small" /></IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                  {pgms.length === 0 && (
                    <TableRow><TableCell colSpan={5} align="center">Nenhuma PGM cadastrada.</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Grid>

          <Grid item xs={12} md={6}>
            <Typography variant="subtitle1" gutterBottom>Zonas</Typography>
            <Stack spacing={1.5} sx={{ mb: 2 }}>
              <Stack direction="row" spacing={1.5}>
                <TextField
                  label="Número (1-99)" type="number" size="small" sx={{ width: 130 }}
                  value={zonaNumero} onChange={e => setZonaNumero(Number(e.target.value))}
                  disabled={zonaEditId !== null}
                  inputProps={{ min: 1, max: 99 }}
                />
                <TextField label="Nome" size="small" fullWidth value={zonaNome} onChange={e => setZonaNome(e.target.value)} />
              </Stack>
              <FormControl size="small" fullWidth>
                <InputLabel>Tipo</InputLabel>
                <Select label="Tipo" value={zonaTipo} onChange={e => setZonaTipo(e.target.value)}>
                  {TIPOS_ZONA.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
                </Select>
              </FormControl>
              <FormControlLabel control={<Switch checked={zonaAtiva} onChange={e => setZonaAtiva(e.target.checked)} />} label="Ativa" />
              <Stack direction="row" spacing={1}>
                <Button variant="contained" size="small" onClick={salvarZona}>{zonaEditId ? 'Atualizar' : 'Adicionar'}</Button>
                {zonaEditId && <Button size="small" onClick={limparFormZona}>Cancelar</Button>}
              </Stack>
            </Stack>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Nº</TableCell><TableCell>Nome</TableCell><TableCell>Tipo</TableCell>
                    <TableCell>Ativa</TableCell><TableCell align="right">Ações</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {zonas.map(z => (
                    <TableRow key={z.id}>
                      <TableCell>{z.numero}</TableCell>
                      <TableCell>{z.nome}</TableCell>
                      <TableCell>{z.tipo}</TableCell>
                      <TableCell>{z.ativa ? 'Sim' : 'Não'}</TableCell>
                      <TableCell align="right">
                        <IconButton size="small" onClick={() => editarZona(z)}><EditIcon fontSize="small" /></IconButton>
                        <IconButton size="small" onClick={() => excluirZona(z.id)}><DeleteIcon fontSize="small" /></IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                  {zonas.length === 0 && (
                    <TableRow><TableCell colSpan={5} align="center">Nenhuma zona cadastrada.</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Grid>
        </Grid>
      </AccordionDetails>
    </Accordion>
  )
}
