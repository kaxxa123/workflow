# Workflow Administrative Access Control

| [Home](../README.md) |
------------------------

``WorkflowBuilder`` and ``WorkflowManager`` are making use of the OpenZeppelin ``AccessControl`` class for managing who can modify the state engine and create WFs.

In both contracts the deployer's account is assigned ``DEFAULT_ADMIN_ROLE``.

Thereafter the deployer can assign the ``DEFAULT_ADMIN_ROLE`` to other accounts: <BR />
```JS
let wf = await WorkflowBuilder.deployed();
let DEFAULT_ADMIN_ROLE = await wf.DEFAULT_ADMIN_ROLE();
await wf.grantRole(DEFAULT_ADMIN_ROLE, accounts[9]);
```

...and in turn anyone with ``DEFAULT_ADMIN_ROLE`` can assign ``WF_SCHEMA_ADMIN_ROLE``/``WF_ADMIN_ROLE``.

```JS
let WF_SCHEMA_ADMIN_ROLE = await wf.WF_SCHEMA_ADMIN_ROLE();
await wf.grantRole( WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[9]});
```

...and revoke ``WF_SCHEMA_ADMIN_ROLE``/``WF_ADMIN_ROLE``:

```JS
await wf.revokeRole(WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[9]});
```

For us to completely hand over the contract we would renounce our admin role:

```JS
await wf.renounceRole(DEFAULT_ADMIN_ROLE, accounts[0], {from: accounts[0]});
```


For more details on OpenZeppelin's ``AccessControl`` check: <BR />
[Using AccessControl.sol - A How-To Guide](https://hackernoon.com/using-accesscontrolsol-a-how-to-guide-0c3c325t)