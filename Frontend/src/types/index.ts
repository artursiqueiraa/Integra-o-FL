export interface Building {
  id: number
  nome: string
  descricao?: string
}

export interface Central {
  id: number
  nome: string
  /** Legado — ver Documentation/ARQUITETURA_SESSION_MANAGER.md. Sem uso na arquitetura atual. */
  ip?: string
  /** Legado — ver {@link ip}. */
  porta?: number
  /** Legado — ver {@link ip}. */
  usuario?: string
  buildingId: number
  buildingNome?: string
  fabricante?: string
  modelo?: string
  firmware?: string
  status?: string
  latencia?: number
  numeroSerie?: string
  ultimoKeepAliveEmUtc?: string
  ultimoIpConectado?: string
  conectadoDesdeUtc?: string
}

export interface History {
  id: number
  data: string
  centralId: number
  pgm: number
  comando: string
  resultado: string
}

// ---- Sessão TCP real (GET/POST /api/centrais/{id}/sessao|log|reconectar|diagnostico) ----

// Campos opcionais do DTO (C# `T?`) chegam pela rede como JSON `null` quando ausentes,
// nunca `undefined` (JSON não tem esse conceito) — por isso `| null` explícito em vez de só `?`,
// para o compilador forçar tratar os dois casos (ver bug corrigido: `!== undefined` não pega
// `null`, e um `.toFixed()`/`.toString()` direto em cima de `null` quebra a tela).
export interface SessaoInfo {
  centralId: number
  statusConexao: 'Online' | 'Offline'
  numeroSerie?: string | null
  modelo?: string | null
  firmware?: string | null
  ipSessao?: string | null
  portaRemota?: number | null
  dataHoraConexaoUtc?: string | null
  ultimoPacoteRecebidoEmUtc?: string | null
  tempoConectadoSegundos?: number | null
  socketConectado: boolean
  handshakeRealizado: boolean
  keepAliveAtivo: boolean
  mac?: string | null
  imei?: string | null
  ultimoKeepAliveEmUtc?: string | null
  ultimoIpConectado?: string | null
  ultimoComando?: string | null
  ultimoSeq?: number | null
  bytesRecebidos?: number | null
  bytesEnviados?: number | null
  latenciaMs?: number | null
  ultimoErro?: string | null
  sessaoAtiva: boolean
  centralCadastrada: boolean
  numeroSerieDivergente: boolean
}

export interface AtividadeLogEntry {
  timestamp: string
  nivel: string
  mensagem: string
  cmd?: number
  seq?: number
}

export interface ReconectarResultado {
  sessaoEncontrada: boolean
  mensagem: string
}

export interface DiagnosticoItem {
  descricao: string
  ok?: boolean | null
  detalhe?: string
}

export interface DiagnosticoResultado {
  centralId: number
  itens: DiagnosticoItem[]
}

// ---- Status ao vivo (GET /api/centrais/{id}/status) ----

export interface Bateria {
  valorBruto: number
  tipo: 'SemBateria' | 'Litio' | 'Chumbo' | 'Carregando' | 'Reservado'
  percentualLitio?: number
  tensaoChumboAproximada?: number
}

export interface Eletrificador {
  estado?: string | null
  permiteDesarmar: boolean
  permiteArmarAway: boolean
}

export interface Problemas {
  bateriaFracaControleOuSensorSemFio: boolean
  supervisaoSensor: boolean
  saidaAuxiliar: boolean
  tamper: boolean
  dhcp: boolean
  caboDeRede: boolean
  moduloCelular: boolean
  sms: boolean
  ethernet: boolean
  gprs: boolean
  linhaTelefonica: boolean
  curto: boolean
  teclado: boolean
  sirene: boolean
  bateria: boolean
  ac: boolean
  bateriaInvertidaOuEmCurto: boolean
  ipDestino2: boolean
  ipDestino1: boolean
  servidorDns: boolean
  redeTecladoAc: boolean
  supervisaoSirene: boolean
  senhaRedeSemFio: boolean
  autenticacaoRedeSemFio: boolean
  ssidNaoEncontrado: boolean
  conflitoIp: boolean
  barramento: boolean
  ddns: boolean
  notificacao: boolean
  moduloEthernet: boolean
  nivelSinalOperadora: boolean
  chipCelular: boolean
  tamperTeclado: boolean
  supervisaoPgm: boolean
  alimentacaoAcNormal: boolean
}

export interface ParticaoStatus {
  numero: number
  estado?: string | null
  desabilitada: boolean
  permiteDesarmar: boolean
  permiteArmar: boolean
  permiteArmarStay: boolean
  permiteArmarAway: boolean
  pronta: boolean
}

export interface ZonaStatus {
  numero: number
  estado?: string | null
  permiteInibir: boolean
}

export interface PgmStatus {
  numero: number
  acionada: boolean
  permitida: boolean
}

export interface CentralStatus {
  centralId: number
  dataHoraCentral?: string
  bateria: Bateria
  eletrificador: Eletrificador
  problemas: Problemas
  particoes: ParticaoStatus[]
  zonas: ZonaStatus[]
  pgms: PgmStatus[]
}

// ---- Comandos de PGM ----

export interface PgmCommandResult {
  pgm: number
  sucesso: boolean
  estadoConfirmado?: boolean | null
  erro?: string
}

// ---- Comandos de Arme (armar/desarmar/stay/away) ----

export interface ArmCommandResult {
  particao: number
  sucesso: boolean
  estadoConfirmado?: boolean | null
  erro?: string
}

// ---- Comandos de Zona (inibir/desinibir) ----

export interface ZoneInhibitResult {
  zona: number
  sucesso: boolean
  inibida?: boolean | null
  erro?: string
}
