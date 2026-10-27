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

## Arquitetura

Única janela (`MainWindow`), em MVVM manual (sem biblioteca externa — INotifyPropertyChanged/ICommand implementados à mão). A lógica de domínio fica isolada em `Model/`, acessada via singletons:

- **`Model/Indicator.cs`** (`Indicator.Instance`) — coração do app. Mantém a `SerialPort` aberta, executa o protocolo de comunicação com o indicador Gefran (handshake binário por endereço de memória, ver `ExecuteInitializationProtocol`/`ReadAddress`/`WriteAndCheckResponse`), e dispara dois timers:
  - `readingTimer` (1 ms) — lê o valor absoluto do indicador continuamente.
  - `storageTimer` (3000 ms, configurável via `StorageTimerInterval`) — grava a leitura atual no `Storage` em intervalos regulares.
  Expõe eventos `ConnectionStateChanged` e `IndicatorValueChanged` que a ViewModel assina para se atualizar (sempre via `Dispatcher.Invoke`, pois os timers disparam fora da UI thread).
- **`Model/SerialPortUtil.cs`** (`SerialPortUtil.Instance`) — enumera/valida portas COM disponíveis e cria instâncias de `SerialPort`.
- **`Model/Storage.cs`** — lista thread-safe (`lock`) de `Reading`s coletadas; notifica `StorageChanged`; exporta para CSV (`;` como separador). Não depende mais de WPF/`Application` — erros de I/O no export propagam para quem chamar `ExportToCSV`.
- **`Model/Reading.cs`** — par imutável (valor lido, tempo decorrido desde o início da coleta).
- **`Model/Exceptions.cs`** — exceções de domínio com mensagens em português já definidas na própria exceção (`SerialPortInvalidException`, `NoResponseFromIndicatorException`, `InvalidResponseFromIndicatorException`, `IndicatorNotReadyException`).

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
