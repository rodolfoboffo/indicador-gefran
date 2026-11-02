# Indicador Gefran

Aplicação desktop (WPF, .NET Framework 4.8) que lê um indicador industrial **Gefran 2351** pela porta serial, exibe o valor atual na tela e permite gravar leituras periódicas para exportação em CSV.

## Funcionalidades

- Leitura contínua do indicador por porta serial (protocolo binário proprietário), com handshake e validação de eco byte a byte.
- Exibição do valor e do tempo decorrido desde o início da coleta.
- Gravação de leituras em memória em um intervalo configurável e exportação para CSV (separador `;`).
- **Reconexão automática**: se a comunicação falhar, a coleta continua e o app tenta reconectar até conseguir ou até o usuário clicar em *Parar*. Durante a queda, o histórico repete a última leitura válida, marcada como `Repetido (sem conexão)`.
- Lista de portas COM atualizada automaticamente quando um dispositivo é conectado ou removido.
- Modo de debug opcional, com logs em arquivo (veja [Modo de debug](#modo-de-debug)).

## Requisitos

- Windows
- .NET Framework 4.8
- SDK do .NET (para `dotnet build`) ou Visual Studio
- Indicador Gefran 2351 conectado a uma porta serial (ou adaptador USB-serial)

## Build e execução

```
cd IndicadorGefran
dotnet build
dotnet run
```

O projeto também abre e compila normalmente no Visual Studio (F5). Não há testes automatizados.

## Uso

1. Selecione a **porta serial** e o **baud rate** (o padrão é 19200).
2. Clique em **Iniciar**. O status da conexão aparece na barra inferior (*Desconectado*, *Conectando...* ou *Conectado*).
3. Ajuste o intervalo de gravação, se necessário. As leituras gravadas aparecem na tabela, com a coluna `Status`.
4. Use **Limpar** para zerar o histórico e **Exportar** para salvar um CSV.
5. Clique em **Parar** para encerrar a coleta.

Baud rates disponíveis: 1200, 2400, 4800, 9600, 19200 e 38400.

## Configuração (`App.config`)

| Chave | Valores | Padrão | Descrição |
|---|---|---|---|
| `SerialTransport` | `Win32` \| `Managed` | `Win32` | Implementação de I/O serial. `Win32` (P/Invoke) permite detectar erro de paridade por byte; `Managed` usa `System.IO.Ports.SerialPort`. Chave ausente ou valor desconhecido usa `Win32`. |
| `InterByteDelayBytes` | número (ponto decimal) | `8.75` | Espera após cada byte enviado, em **tempos de byte**. Independe do baud rate: o tempo real é calculado a partir do baud configurado na porta. |
| `DebugModeEnabled` | `True` \| `False` | `False` | Liga o log geral e exibe os controles de debug na interface. |

As chaves são lidas na inicialização; alterações exigem reiniciar o app.

## Modo de debug

Com `DebugModeEnabled=True`, o app grava, na pasta do executável:

- `app_<yyyyMMdd_HHmmss>.log` — log geral (um por execução): início e fim do app, exceções não tratadas, `Start`/`Stop`, mudanças de estado da conexão, falhas de conexão (com bytes esperados × lidos ou tempo de espera), export CSV, etc.
- `serialport_<yyyyMMdd_HHmmss>.log` — I/O da porta serial (abertura/fechamento e bytes enviados/recebidos em hexa). Ligado pelo checkbox **Log da porta serial**, que só aparece em modo debug. Bytes com erro de paridade aparecem marcados com `*`.

Todas as linhas têm timestamp em UTC.

## Estrutura do projeto

```
IndicadorGefran/
├── Model/        Lógica de domínio: Indicator (protocolo e reconexão), transportes seriais,
│                 Storage, Reading, loggers, exceções
├── ViewModel/    MVVM manual: MainViewModel, RelayCommand, ViewModelBase
├── Services/     DialogService e SerialPortMonitorService (WM_DEVICECHANGE)
├── MainWindow.*  Janela única
└── App.config    Configurações
Documentacao/     Material de referência do indicador Gefran 2351
```

Para detalhes de arquitetura e do protocolo, consulte [`IndicadorGefran/CLAUDE.md`](IndicadorGefran/CLAUDE.md).

## Contexto técnico: ruído na linha

A comunicação serial com o indicador é sensível a ruído, principalmente quando se transmite enquanto o eco de bytes anteriores ainda está chegando (full-duplex). Por isso o app envia **um byte por vez**, espera o fim da transmissão e um intervalo configurável (`InterByteDelayBytes`) antes do próximo, e valida o eco de cada comando. Qualquer divergência dispara a reconexão automática.
