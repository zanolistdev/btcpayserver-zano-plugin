# Zano BTCPay Server Plugin

A comprehensive plugin for BTCPay Server that enables Zano cryptocurrency payments with advanced blockchain monitoring and automated payment processing.

## 🚀 Overview

The Zano BTCPay Server Plugin integrates Zano blockchain functionality into BTCPay Server, providing:

- **Automated Payment Processing**: Real-time blockchain monitoring with configurable polling intervals
- **Multi-Network Support**: Support for multiple Zano networks (mainnet, testnet)
- **Advanced Confirmation Logic**: Configurable confirmation thresholds based on speed policies
- **Event-Driven Architecture**: Seamless integration with BTCPay Server's event system
- **RPC Integration**: Direct communication with Zano daemon and wallet nodes

## ✨ Features

### Core Functionality
- **Blockchain Monitoring**: Polls for new blocks every 3 seconds (configurable)
- **Payment Detection**: Automatically detects and processes incoming Zano payments
- **Invoice Management**: Real-time invoice status updates and payment confirmations
- **Multi-Address Support**: Generates and manages multiple payment addresses per invoice

### Payment Processing
- **Confirmation Policies**: 
  - High Speed: 0 confirmations
  - Medium Speed: 1 confirmation  
  - Low-Medium Speed: 2 confirmations
  - Low Speed: 6 confirmations
- **Custom Thresholds**: Store-specific confirmation requirements
- **Lock Time Support**: Handles time-locked transactions

### Technical Features
- **Event-Driven Architecture**: Built on BTCPay Server's event system
- **RPC Client Management**: Robust daemon and wallet RPC communication
- **Error Handling**: Graceful handling of network failures and RPC errors
- **Logging**: Comprehensive logging for debugging and monitoring

## 🏗️ Architecture

### Service Components

#### ZanoListener
The core service that monitors the blockchain and processes payments:
- **Block Polling**: Timer-based polling every 3 seconds
- **Event Processing**: Handles Zano blockchain events
- **Payment Updates**: Automatically updates payment states and confirmations
- **Invoice Activation**: Activates payment methods when sufficient confirmations are received

#### ZanoRPCProvider
Manages RPC connections to Zano nodes:
- **Daemon RPC**: Block height and network information
- **Wallet RPC**: Transaction details and transfer information
- **Connection Management**: Handles multiple network connections

#### Payment Handlers
Process Zano-specific payment data:
- **Payment Parsing**: Converts blockchain data to payment entities
- **Status Management**: Determines payment status based on confirmations
- **Amount Conversion**: Handles Zano's atomic units

## 📋 Prerequisites

- **BTCPay Server**: Version 1.12.0 or higher
- **Zano Node**: Running Zano daemon and wallet RPC
- **.NET 6.0+**: For building and running the plugin
- **PostgreSQL**: For BTCPay Server database

## 🛠️ Installation

### 1. Clone the Repository
```bash
git clone https://github.com/your-username/ZanoGitHub.git
cd ZanoGitHub
```

### 2. Build the Plugin
```bash
cd Plugins/Zano
dotnet build
```

### 3. Deploy to BTCPay Server
```bash
# Copy the built plugin to BTCPay Server plugins directory
cp -r bin/Debug/net6.0/* /path/to/btcpayserver/plugins/Zano/
```

### 4. Restart BTCPay Server
```bash
# Restart BTCPay Server to load the plugin
sudo systemctl restart btcpayserver
```

## ⚙️ Configuration

### Plugin Settings
Configure the plugin through BTCPay Server's plugin settings:

```json
{
  "Zano": {
    "Network": "mainnet",
    "DaemonRpcUrl": "http://localhost:32348",
    "WalletRpcUrl": "http://localhost:32349",
    "Username": "your-rpc-username",
    "Password": "your-rpc-password",
    "BlockPollingInterval": 3
  }
}
```

### Environment Variables
You can also configure the plugin using environment variables:

```bash
# Zano Daemon RPC endpoint
export ZANO_DAEMON_URI="http://37.27.100.59:10500"

# Zano Wallet RPC endpoint  
export ZANO_WALLET_DAEMON_URI="http://127.0.0.1:11233"
```

**Note**: Environment variables take precedence over configuration file settings.

### Zano Node Configuration
Ensure your Zano node has RPC enabled:

```bash
# In zano.conf
rpc-bind-ip=0.0.0.0
rpc-bind-port=32348
rpc-login=username:password
```

## 🔧 Usage

### 1. Enable the Plugin
- Go to BTCPay Server admin panel
- Navigate to Plugins > Zano
- Click "Enable"

### 2. Configure Payment Methods
- Go to Store Settings > Payment Methods
- Add Zano as a payment method
- Configure confirmation thresholds and speed policies

### 3. Create Invoices
- Create invoices as usual
- Zano payment addresses will be automatically generated
- Monitor payment status in real-time

### 4. Monitor Payments
The plugin automatically:
- Detects new blocks every 3 seconds
- Processes incoming transactions
- Updates payment confirmations
- Activates invoices when sufficient confirmations are received

## 📊 Monitoring

### Logs
Monitor plugin activity through BTCPay Server logs:
```bash
# View plugin logs
tail -f /var/log/btcpayserver/btcpayserver.log | grep Zano
```

### Key Metrics
- **Block Polling**: Every 3 seconds
- **Payment Processing**: Real-time as blocks arrive
- **Confirmation Updates**: Automatic status updates
- **Error Handling**: Graceful degradation on failures

## 🔍 Troubleshooting

### Common Issues

#### RPC Connection Failed
```bash
# Check Zano node status
curl -X POST http://localhost:32348/json_rpc \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":"0","method":"getinfo"}'
```

#### Plugin Not Loading
- Verify .NET version compatibility
- Check plugin file permissions
- Review BTCPay Server logs for errors

#### Payment Not Detected
- Verify RPC credentials
- Check network connectivity
- Ensure sufficient confirmations for speed policy

### Debug Mode
Enable debug logging in plugin configuration:
```json
{
  "Logging": {
    "LogLevel": {
      "BTCPayServer.Plugins.Zano": "Debug"
    }
  }
}
```

## 🧪 Testing

### Testnet Setup
```bash
# Configure for testnet
"Network": "testnet",
"DaemonRpcUrl": "http://localhost:32348",
"WalletRpcUrl": "http://localhost:32349"
```

### Test Scenarios
- Create test invoices
- Send test payments
- Verify confirmation counting
- Test speed policy enforcement

## 🤝 Contributing

### Development Setup
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

### Code Style
- Follow C# coding conventions
- Use async/await for I/O operations
- Implement proper error handling
- Add comprehensive logging

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **BTCPay Server Team**: For the excellent plugin architecture
- **Zano Community**: For blockchain integration support
- **Contributors**: All who have helped improve this plugin

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/your-username/ZanoGitHub/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-username/ZanoGitHub/discussions)
- **Documentation**: [Wiki](https://github.com/your-username/ZanoGitHub/wiki)

## 🔄 Version History

### v1.0.0
- Initial release
- Basic Zano payment support
- Block polling every 3 seconds
- Multi-network support
- Advanced confirmation logic

---

**Note**: This plugin is designed for production use but should be thoroughly tested in your environment before deployment.
