# Clone, Configure, Build, Deploy

| [Home](../README.md) |
------------------------

This repository includes three projects:

|||
|--------------|------------------------------------------------------|
| __workflow__ | A truffle project for the workflow smart contracts.  |
| __WFAPI__    | A .Net Core 3.1 library wrapping smart contracts and blockchain access.  |
| __WFTest__   | A .Net Core 3.1 console client application.          |
|||

<BR />

## 1. Clone

```BASH
git clone https://github.com/kaxxa123/workflow.git
```

<BR />

## 2. Configure

We need to customize the projects with a couple of secrets:

1. An [Infura](https://infura.io/) API Key

1. A MNEMONIC for deriving the contract deployer account.

Next we set these at the C# __WFTest__ and  the truffle __workflow__ project.

<BR />


### 2.1. Configure C# WFTest Project

1. Under the __WFTest__ folder create a file named ``config.json``

1. Configure the Infura API key value as follows:
    ```JSON
    {
        "infuraKey": "1234...."
    }
    ```

<BR />


### 2.2. Configure Truffle Workflow Project

1. Under __workflow__ folder create a new folder named ``extra``

1. Create two files named: <BR />
``./workflow/extra/.secret1`` <BR />
``./workflow/extra/.secret2``

1. Copy the contract deployer MNEMONIC to ``.secret1``. This would usually look something like this: <BR />
    ``security crime belt rib ball crystal upgrade bike subway penalty ability bird``

    Feel free to use the above mnemonic, for local testing just don't deposit anything valuable to it. It's corresponding address is: <BR />
    ``0x256EF9b35afd9b00E5C9660DbBc92243f8940D70``

1. Copy the full Infura connection URL to ``.secret2``. This would usually look something like this: <BR />
    https://ropsten.infura.io/v3/1234....

<BR />



## 3. Build

<BR />

### 3.1. Building .Net Projects

1. Install .Net Core 3.1, check if this is already installed using:
    ```BASH
    dotnet --list-sdks
    ```

1. Build __WFTest__ (this will also build __WFAPI__):

    ```BASH
    cd WFTest
    dotnet build
    ```

<BR />

### 3.1. Building truffle Project

1. Install the npm packages and compile the smart contracts:

    ```BASH
    npm i
    truffle compile
    ```

1. Run the mocha tests using:
    ```BASH
    truffle test
    ```

<BR />


## 4. Deploy

1. Depending on which chain you would like to deploy update file ``truffle-config.js``

2. Make sure the account configure under ``.secret1`` has the necessary credit to pay for transaction fees.

3. To deploy to a local geth installation allowing connections over http://127.0.0.1:8545
    ```
    truffle deploy --network geth
    ```

<BR />
