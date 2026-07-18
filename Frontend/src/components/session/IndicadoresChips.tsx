import { Chip, Stack } from '@mui/material'
import { SessaoInfo } from '../../types'

interface Props {
  sessao: SessaoInfo
}

export default function IndicadoresChips({ sessao }: Props) {
  return (
    <Stack direction="row" flexWrap="wrap" gap={1}>
      <Chip
        size="small"
        label={sessao.sessaoAtiva ? '🟢 Sessão ativa' : '🔴 Sem sessão'}
        color={sessao.sessaoAtiva ? 'success' : 'error'}
        variant="outlined"
      />

      {sessao.sessaoAtiva && (
        <Chip
          size="small"
          label={sessao.handshakeRealizado ? '🟢 Handshake' : '🔴 Handshake pendente'}
          color={sessao.handshakeRealizado ? 'success' : 'error'}
          variant="outlined"
        />
      )}

      {sessao.sessaoAtiva && (
        <Chip
          size="small"
          label={sessao.keepAliveAtivo ? '🟢 KeepAlive' : '🔴 KeepAlive expirado'}
          color={sessao.keepAliveAtivo ? 'success' : 'error'}
          variant="outlined"
        />
      )}

      {sessao.ultimoComando && (
        <Chip size="small" label="🟢 Status sincronizado" color="success" variant="outlined" />
      )}

      <Chip
        size="small"
        label={sessao.centralCadastrada ? '🟢 Central cadastrada' : '🔴 Central não cadastrada'}
        color={sessao.centralCadastrada ? 'success' : 'error'}
        variant="outlined"
      />

      {sessao.numeroSerieDivergente && (
        <Chip size="small" label="🔴 Número de série divergente" color="error" variant="outlined" />
      )}
    </Stack>
  )
}
