import { useState } from 'react'
import api from '../../services/api'
import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, TextField, Button, Stack, Typography, Alert, Chip
} from '@mui/material'

interface CampoAnalisado {
  nome: string
  offset: number
  tamanho: number
  valorBrutoHex: string
  valorInterpretado: string
  descricao?: string
}

interface PacoteAnalisado {
  cabecalhoValido: boolean
  cab?: number
  qde?: number
  seq?: number
  cmd?: number
  cmdNome?: string
  checksumValido?: boolean
  campos: CampoAnalisado[]
  avisos: string[]
}

/**
 * Ferramenta de desenvolvimento (Fase 0.2 do plano de homologação) — cola um pacote JFL em
 * hex e mostra a decomposição campo a campo. Não é uma tela operacional do produto.
 */
export default function PacketInspectorPage() {
  const [hex, setHex] = useState('')
  const [resultado, setResultado] = useState<PacoteAnalisado | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)

  const analisar = async () => {
    setErro(null)
    setResultado(null)
    if (!hex.trim()) {
      setErro('Cole os bytes do pacote em hexadecimal (ex.: 7B 05 18 40 26).')
      return
    }

    setCarregando(true)
    try {
      const response = await api.post<PacoteAnalisado>('/dev/packet-inspector/analisar', { hex })
      setResultado(response.data)
    } catch {
      setErro('Não foi possível analisar o pacote — confira se o Backend está rodando.')
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div>
      <Typography variant="h5" gutterBottom>Inspetor de Pacotes JFL</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Ferramenta de desenvolvimento — cole um pacote em hexadecimal (com ou sem espaços) e veja a
        decomposição campo a campo, validação de checksum e identificação do comando.
      </Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 3 }} alignItems="flex-start">
        <TextField
          label="Pacote em hex"
          placeholder="7B 05 18 40 26"
          value={hex}
          onChange={e => setHex(e.target.value)}
          fullWidth
          multiline
          minRows={2}
        />
        <Button variant="contained" onClick={analisar} disabled={carregando}>Analisar</Button>
      </Stack>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}

      {resultado && (
        <>
          <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
            {resultado.cmdNome && <Chip label={`CMD: ${resultado.cmdNome}`} color="primary" />}
            {resultado.seq !== undefined && <Chip label={`SEQ: 0x${resultado.seq.toString(16).toUpperCase()}`} />}
            <Chip
              label={resultado.checksumValido ? 'Checksum OK' : 'Checksum inválido'}
              color={resultado.checksumValido ? 'success' : 'error'}
            />
          </Stack>

          {resultado.avisos.length > 0 && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              {resultado.avisos.map((a, i) => <div key={i}>{a}</div>)}
            </Alert>
          )}

          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Campo</TableCell>
                  <TableCell>Offset</TableCell>
                  <TableCell>Tamanho</TableCell>
                  <TableCell>Bruto (hex)</TableCell>
                  <TableCell>Valor interpretado</TableCell>
                  <TableCell>Descrição</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {resultado.campos.map((c, i) => (
                  <TableRow key={i}>
                    <TableCell>{c.nome}</TableCell>
                    <TableCell>{c.offset >= 0 ? c.offset : '—'}</TableCell>
                    <TableCell>{c.tamanho}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{c.valorBrutoHex}</TableCell>
                    <TableCell>{c.valorInterpretado}</TableCell>
                    <TableCell>{c.descricao}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}
    </div>
  )
}
