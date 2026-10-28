# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Aplicação WPF (.NET Framework 4.8, C#) que lê um indicador industrial Gefran 2351 via porta serial (protocolo proprietário binário), exibe o valor atual na tela e permite gravar leituras periódicas em memória para posterior exportação em CSV.

## Build / run

Projeto único, SDK-style (`IndicadorGefran.csproj`, `net48`, `UseWPF`). Não há testes automatizados nem lint configurado neste repositório.

```
dotnet build
dotnet run
```

Também abre e builda normalmente no Visual Studio (F5).

`dotnet build` não roda os custom tools de `Resources.resx`/`Settings.settings` (isso só acontece dentro do Visual Studio) — então `Properties/Resources.Designer.cs` e `Properties/Settings.Designer.cs` precisam ser editados manualmente junto com o `.resx`/`.settings` correspondente quando adicionar/alterar recursos ou settings.

## Arquitetura

Única janela (`MainWindow`), em MVVM manual (sem biblioteca externa — INotifyPropertyChanged/ICommand implementados à mão). A lógica de domínio fica isolada em `Model/`, acessada via singletons:

- **`Model/Indicator.cs`** (`Indicator.Instance`) — coração do app. Mantém a porta aberta (via `ISerialPortTransport`, não `SerialPort` diretamente), executa o protocolo de comunicação com o indicador Gefran (handshake binário por endereço de memória, ver `ExecuteInitializationProtocol`/`ReadAddress`/`WriteAndCheckResponse`). Separa dois conceitos de estado:
  - `IsRunning` (bool) — **intenção**: liga em `Start(portName, baudRate)`, só desliga em `Stop()` explícito. Independe de quantas reconexões aconteçam no meio.
  - `ConnectionState` (enum `IndicatorConnectionState`: `Disconnected`/`Connecting`/`Connected`) — **estado real** da porta/handshake. `SetConnectionState` só dispara `ConnectionStateChanged` quando o valor muda de fato.
  Qualquer falha de protocolo durante a 1ª tentativa ou durante leitura ativa (`ReaAbsoluteCountValue`/`ReadDisplayValue`) não desliga `IsRunning` — só chama `HandleConnectionLost()`/mantém `ConnectionState=Connecting`, e um `reconnectTimer` (fixo em 5ms, não configurável/sem UI) vai tentando `AttemptConnect()` de novo indefinidamente até conseguir ou até `Stop()` ser chamado. O relógio (`initialTime`) só é resetado em `Start()`, nunca numa reconexão — a coleta continua de onde parou, sem zerar o tempo.
  `readingTimer` e `storageTimer` ficam ativos durante toda a sessão `Running` (ligados em `Start`, desligados só em `Stop`) — **não** são pausados durante uma reconexão. A cada tick, `OnReadingTimerElapsed` só efetivamente tenta ler (`ReaAbsoluteCountValue`) se `ConnectionState == Connected` (senão é no-op: não atualiza `CurrentValue`/`CurrentTime`); já `OnStorageTimerElapsed` sempre grava a última leitura conhecida (`this.reading`) no histórico, mesmo em `Connecting` — ou seja, durante uma reconexão o histórico continua recebendo amostras repetindo o último valor válido (marcadas como `Reading.IsStale`), em vez de ficar com um buraco (só não grava nada se `this.reading` ainda for `null`, ou seja, antes da 1ª leitura bem-sucedida da sessão). Ambos são ligados/desligados sempre em pares com try/finally nos handlers de tick, pra garantir que uma excessão no meio não deixe o timer travado desligado. Já `reconnectTimer` é controlado centralizadamente em `OnIndicatorConnectionStateChanged` (reage à mudança de `ConnectionState`): só roda quando `Running` e não `Connected`.
  Expõe eventos `ConnectionStateChanged` e `IndicatorValueChanged` que a ViewModel assina para se atualizar (sempre via `Dispatcher.Invoke`, pois os timers disparam fora da UI thread).
- **`Model/SerialPortUtil.cs`** (`SerialPortUtil.Instance`) — enumera/valida portas COM disponíveis (via `SerialPort.GetPortNames()`, estático) e é a fábrica de `ISerialPortTransport` (`GetSerialPort`), decidindo a implementação pela chave `SerialTransport` em `App.config` (`appSettings`).
- **`App.config` (`appSettings`)** — além de `SerialTransport`, tem `DebugModeEnabled` (`True`/`False`), lida uma vez no construtor da `MainViewModel` (`ConfigurationManager.AppSettings["DebugModeEnabled"]`) e exposta como `DebugControlsVisibility` (`Visibility.Visible`/`Collapsed`). Controla a visibilidade de controles de debug na UI — hoje só o checkbox "Log da porta serial" (`checkboxSerialLogging`, em `MainWindow.xaml`); novos controles de debug devem reusar a mesma propriedade.
- **`Model/ISerialPortTransport.cs`** / **`Model/ManagedSerialPortTransport.cs`** / **`Model/Win32SerialPortTransport.cs`** — abstração de I/O serial com duas implementações alternáveis via `App.config` (`<add key="SerialTransport" value="Managed|Win32" />`, default `Managed`):
  - `ManagedSerialPortTransport` — wrapper fino sobre `System.IO.Ports.SerialPort` (comportamento histórico). `ReadByte` nunca reporta erro de paridade (`SerialPort` não expõe isso — o driver já substitui o byte corrompido por `0x3F` antes da aplicação ver).
  - `Win32SerialPortTransport` — implementação em P/Invoke puro (`CreateFile`/`ReadFile`/`WriteFile`/`ClearCommError` de `kernel32.dll`), com `fErrorChar=false` na `DCB` (não deixa o driver mascarar o byte). Chama `ClearCommError` imediatamente antes e depois de cada `ReadFile` de 1 byte, na tentativa de correlacionar a flag `CE_RXPARITY` a esse byte específico — só faz sentido porque a leitura já é byte a byte (`GetResponse`); não seria nem tentável com `SerialPort`, que busca bytes em background de forma oportunista. **Limitação observada em campo**: com adaptador USB-serial, a correlação não é perfeitamente atômica — em bytes que chegam rápido (baud alto), o flag de paridade pode aparecer no byte *seguinte* ao que realmente teve o erro, por causa da latência do driver/USB entre o byte chegar ao buffer e a chamada a `ClearCommError` perceber isso. Ou seja, o `*` no log é um indício "best-effort" de ruído na linha, não uma localização garantida do byte exato — a validação de eco em `WriteAndCheckResponse` (que já existia antes dessa feature) continua sendo o mecanismo real que detecta corrupção e desconecta.
  - Ambas lançam `System.TimeoutException` em timeout de leitura (mesmo contrato de `SerialPort.ReadByte()`), preservando o `catch (TimeoutException)` em `GetResponse`.
- **`Model/Storage.cs`** — lista thread-safe (`lock`) de `Reading`s coletadas; notifica `StorageChanged`; exporta para CSV (`;` como separador, incluindo a coluna `Status`). Não depende mais de WPF/`Application` — erros de I/O no export propagam para quem chamar `ExportToCSV`.
- **`Model/Reading.cs`** — imutável (valor lido, tempo decorrido desde o início da coleta, flag `IsStale`). `IsStale=true` marca uma amostra que não é uma leitura nova — é a repetição da última leitura válida, gravada pelo `storageTimer` enquanto `ConnectionState` não é `Connected` (ver `Indicator.OnStorageTimerElapsed`/`Reading.AsStale()`). `Status` ("OK"/"Repetido (sem conexão)") é exibido na coluna extra do `DataGrid` e no CSV exportado.
- **`Model/Exceptions.cs`** — exceções de domínio com mensagens em português já definidas na própria exceção (`SerialPortInvalidException`, `NoResponseFromIndicatorException`, `InvalidResponseFromIndicatorException`, `IndicatorNotReadyException`).
- **`Model/SerialPortLogger.cs`** (`SerialPortLogger.Instance`) — logger de debug opcional para I/O da porta serial. Quando `Enabled`, abre um `StreamWriter` em `serialport_<yyyyMMdd_HHmmss>.log` (pasta do executável, `AppDomain.CurrentDomain.BaseDirectory`) e registra, com timestamp UTC: abertura/fechamento de porta e bytes enviados/recebidos em hex — sempre acompanhados da config da porta no formato `Porta,BaudRate,Paridade,StopBits` (ex.: `COM3,9600,E,1`). Bytes recebidos com erro de paridade (só detectável com `Win32SerialPortTransport`) aparecem marcados com `*` (ex.: `45*`). Instrumentado dentro de `Indicator.OpenPort`/`ClosePort`/`WriteSerial`/`GetResponse`, então cobre toda a comunicação sem precisar tocar nos métodos de protocolo (`ReadAddress`, `SendAddress`, etc.). Controlado pelo toggle `IsSerialLoggingEnabled` na `MainViewModel` — **não** persiste entre execuções (sempre inicia desabilitado); `Properties/Settings.settings` está vazio de propósito, nenhuma configuração de usuário é salva em disco hoje.

### Camada MVVM

- **`ViewModel/ViewModelBase.cs`** — `INotifyPropertyChanged` + `SetProperty<T>` (com comparação de igualdade) e `OnPropertyChanged`.
- **`ViewModel/RelayCommand.cs`** — `ICommand` manual (`Action`/`Predicate`, `CanExecuteChanged` ligado a `CommandManager.RequerySuggested`).
- **`ViewModel/MainViewModel.cs`** — única ViewModel do app. Assina os eventos do `Indicator`/`Storage` e expõe propriedades bindable (portas, baud rates, `StartStopButtonLabel` "Iniciar"/"Parar" conforme `IsRunning`, `ConnectionStatusText` "Desconectado"/"Conectando..."/"Conectado" conforme `ConnectionState`, valor/tempo atual, texto dos intervalos de storage/reconexão, coleção de leituras) e comandos (`StartStopCommand`, `ClearCommand`, `ExportCommand`). Como `Indicator.Start` não lança mais por falha de protocolo (ela mesma tenta de novo internamente), `ExecuteStartStop` não precisa mais de `catch` para erro de conexão — só valida porta selecionada antes de chamar `Start`. Depende de `IDialogService` em vez de chamar `MessageBox`/`SaveFileDialog` diretamente — mantém a ViewModel testável e sem referência direta a WPF para diálogos.
- **`Services/IDialogService.cs` / `Services/DialogService.cs`** — abstrai `MessageBox.Show` (alerta/erro/info) e o `SaveFileDialog` de exportação CSV. `App.xaml.cs` não tem mais esses helpers (ficaram só na `DialogService`). `DialogService` é singleton (`Instance`), mesmo padrão de `Indicator`/`SerialPortUtil`/`SerialPortLogger`/`SerialPortMonitorService`.
- **`Services/ISerialPortMonitorService.cs` / `Services/SerialPortMonitorService.cs`** — singleton que escuta `WM_DEVICECHANGE` (via `HwndSource.AddHook`, sem `RegisterDeviceNotification`) e dispara `PortsChanged` sempre que um dispositivo é conectado/removido do Windows, permitindo atualizar a lista de portas COM automaticamente — não existe mais um botão "Atualizar" manual na UI, essa é a única forma de atualização da lista agora. `Attach(Window)`/`Detach()` só existem na classe concreta (chamados em `MainWindow.xaml.cs`, em `SourceInitialized`/`Closing`) — a interface só expõe o evento, pra não acoplar `HwndSource`/`Window` na ViewModel.

### View

- **`App.xaml.cs`** — vazio (apenas a classe `Application` gerada).
- **`MainWindow.xaml(.cs)`** — toolbar para selecionar porta/baud rate e iniciar/parar, `StatusBar` na parte inferior da janela com o status da conexão (`ConnectionStatusText`), `DataGrid` com o histórico de leituras (incluindo a coluna `Status`), labels grandes com o valor/tempo atual, botões Limpar/Exportar. Code-behind cria a `MainViewModel` (injetando `DialogService.Instance` e `SerialPortMonitorService.Instance`) como `DataContext`, encaminha `Closing` para `MainViewModel.OnWindowClosing()` (e chama `SerialPortMonitorService.Instance.Detach()`), e em `SourceInitialized` chama `SerialPortMonitorService.Instance.Attach(this)` (precisa do HWND já existir). Todo o resto é binding (`Command`, `ItemsSource`, `SelectedItem`, `Text`, `Content`, `IsEnabled`).

### Protocolo do indicador (em `Indicator.cs`)

Sequência fixa: abre a porta com paridade Even, envia reset (`0x55`), reabre com paridade Odd, seleciona o slave, lê o endereço `GEFRAN_ADDRESS` esperando a string "GEFRAN" como confirmação. Leituras subsequentes usam `ReadAddress` (envia comando de leitura + número de bytes + endereço, lê resposta byte a byte com timeout de 1s). Qualquer exceção durante a leitura chama `Stop()` e propaga.

### Fluxo de erros/estado de conexão

Ver seção acima (`Model/Indicator.cs`) — falha de protocolo nunca mais desliga `IsRunning` sozinha; só reconexão automática via `reconnectTimer` até sucesso ou `Stop()` explícito pelo usuário. A UI não mostra mais `MessageBox` para falhas de conexão/protocolo — só o texto `ConnectionStatusText` ("Conectando...") e as linhas do `SerialPortLogger`, se habilitado.
