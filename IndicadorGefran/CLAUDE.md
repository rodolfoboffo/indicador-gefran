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

- **`Model/Indicator.cs`** (`Indicator.Instance`) — coração do app. Mantém a porta aberta (via `ISerialPortTransport`, não `SerialPort` diretamente), executa o protocolo de comunicação com o indicador Gefran (handshake binário por endereço de memória, ver `ExecuteInitializationProtocol`/`ReadAddress`/`WriteAndCheckResponse`), e dispara dois timers:
  - `readingTimer` (1 ms) — lê o valor absoluto do indicador continuamente.
  - `storageTimer` (3000 ms, configurável via `StorageTimerInterval`) — grava a leitura atual no `Storage` em intervalos regulares.
  Expõe eventos `ConnectionStateChanged` e `IndicatorValueChanged` que a ViewModel assina para se atualizar (sempre via `Dispatcher.Invoke`, pois os timers disparam fora da UI thread).
- **`Model/SerialPortUtil.cs`** (`SerialPortUtil.Instance`) — enumera/valida portas COM disponíveis (via `SerialPort.GetPortNames()`, estático) e é a fábrica de `ISerialPortTransport` (`GetSerialPort`), decidindo a implementação pela chave `SerialTransport` em `App.config` (`appSettings`).
- **`Model/ISerialPortTransport.cs`** / **`Model/ManagedSerialPortTransport.cs`** / **`Model/Win32SerialPortTransport.cs`** — abstração de I/O serial com duas implementações alternáveis via `App.config` (`<add key="SerialTransport" value="Managed|Win32" />`, default `Managed`):
  - `ManagedSerialPortTransport` — wrapper fino sobre `System.IO.Ports.SerialPort` (comportamento histórico). `ReadByte` nunca reporta erro de paridade (`SerialPort` não expõe isso — o driver já substitui o byte corrompido por `0x3F` antes da aplicação ver).
  - `Win32SerialPortTransport` — implementação em P/Invoke puro (`CreateFile`/`ReadFile`/`WriteFile`/`ClearCommError` de `kernel32.dll`), com `fErrorChar=false` na `DCB` (não deixa o driver mascarar o byte). Chama `ClearCommError` imediatamente antes e depois de cada `ReadFile` de 1 byte, então a flag `CE_RXPARITY` fica correlacionada com certeza a esse byte específico — só funciona porque a leitura já é byte a byte (`GetResponse`); não seria confiável com `SerialPort`, que busca bytes em background de forma oportunista.
  - Ambas lançam `System.TimeoutException` em timeout de leitura (mesmo contrato de `SerialPort.ReadByte()`), preservando o `catch (TimeoutException)` em `GetResponse`.
- **`Model/Storage.cs`** — lista thread-safe (`lock`) de `Reading`s coletadas; notifica `StorageChanged`; exporta para CSV (`;` como separador). Não depende mais de WPF/`Application` — erros de I/O no export propagam para quem chamar `ExportToCSV`.
- **`Model/Reading.cs`** — par imutável (valor lido, tempo decorrido desde o início da coleta).
- **`Model/Exceptions.cs`** — exceções de domínio com mensagens em português já definidas na própria exceção (`SerialPortInvalidException`, `NoResponseFromIndicatorException`, `InvalidResponseFromIndicatorException`, `IndicatorNotReadyException`).
- **`Model/SerialPortLogger.cs`** (`SerialPortLogger.Instance`) — logger de debug opcional para I/O da porta serial. Quando `Enabled`, abre um `StreamWriter` em `serialport_<yyyyMMdd_HHmmss>.log` (pasta do executável, `AppDomain.CurrentDomain.BaseDirectory`) e registra, com timestamp UTC: abertura/fechamento de porta e bytes enviados/recebidos em hex — sempre acompanhados da config da porta no formato `Porta,BaudRate,Paridade,StopBits` (ex.: `COM3,9600,E,1`). Bytes recebidos com erro de paridade (só detectável com `Win32SerialPortTransport`) aparecem marcados com `*` (ex.: `45*`). Instrumentado dentro de `Indicator.OpenPort`/`ClosePort`/`WriteSerial`/`GetResponse`, então cobre toda a comunicação sem precisar tocar nos métodos de protocolo (`ReadAddress`, `SendAddress`, etc.). Controlado pelo toggle `IsSerialLoggingEnabled` na `MainViewModel`, persistido em `Properties/Settings.settings` (`SerialPortLoggingEnabled`).

### Camada MVVM

- **`ViewModel/ViewModelBase.cs`** — `INotifyPropertyChanged` + `SetProperty<T>` (com comparação de igualdade) e `OnPropertyChanged`.
- **`ViewModel/RelayCommand.cs`** — `ICommand` manual (`Action`/`Predicate`, `CanExecuteChanged` ligado a `CommandManager.RequerySuggested`).
- **`ViewModel/MainViewModel.cs`** — única ViewModel do app. Assina os eventos do `Indicator`/`Storage` e expõe propriedades bindable (portas, baud rates, estado de conexão, valor/tempo atual, texto do intervalo, coleção de leituras) e comandos (`ConnectDisconnectCommand`, `RefreshPortsCommand`, `ClearCommand`, `ExportCommand`). Depende de `IDialogService` em vez de chamar `MessageBox`/`SaveFileDialog` diretamente — mantém a ViewModel testável e sem referência direta a WPF para diálogos.
- **`Services/IDialogService.cs` / `Services/DialogService.cs`** — abstrai `MessageBox.Show` (alerta/erro/info) e o `SaveFileDialog` de exportação CSV. `App.xaml.cs` não tem mais esses helpers (ficaram só na `DialogService`).

### View

- **`App.xaml.cs`** — vazio (apenas a classe `Application` gerada).
- **`MainWindow.xaml(.cs)`** — toolbar para selecionar porta/baud rate e conectar/desconectar, `DataGrid` com o histórico de leituras, labels grandes com o valor/tempo atual, botões Limpar/Exportar. Code-behind só cria a `MainViewModel` (injetando `DialogService`) como `DataContext` e encaminha o evento `Closing` para `MainViewModel.OnWindowClosing()`; todo o resto é binding (`Command`, `ItemsSource`, `SelectedItem`, `Text`, `Content`, `IsEnabled`).

### Protocolo do indicador (em `Indicator.cs`)

Sequência fixa: abre a porta com paridade Even, envia reset (`0x55`), reabre com paridade Odd, seleciona o slave, lê o endereço `GEFRAN_ADDRESS` esperando a string "GEFRAN" como confirmação. Leituras subsequentes usam `ReadAddress` (envia comando de leitura + número de bytes + endereço, lê resposta byte a byte com timeout de 1s). Qualquer exceção durante a leitura chama `Stop()` e propaga.

### Fluxo de erros/estado de conexão

`Indicator.IsReady` reflete se a porta está aberta. Mudanças de estado (`Initialize`/`Stop`) disparam `ConnectionStateChanged`, que liga/desliga os timers e atualiza a UI (habilita/desabilita combos, troca o texto do botão Conectar/Desconectar).
