const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;

contract('Testing Adding States Failures', function (accounts) {

    it('Should create new Admin', async () => {
        let wf = await WorkflowBuilder.deployed();

        let DEFAULT_ADMIN_ROLE = await wf.DEFAULT_ADMIN_ROLE();
        await wf.grantRole(DEFAULT_ADMIN_ROLE, accounts[9]);
        await wf.renounceRole(DEFAULT_ADMIN_ROLE, accounts[0]);
    });

    it('Should fail to grant Schema Admin', async () => {
        let wf = await WorkflowBuilder.deployed();

        let WF_SCHEMA_ADMIN_ROLE = await wf.WF_SCHEMA_ADMIN_ROLE();

        //Non-Admin cannot grant roles
        await HlpFail.testFail("wf.grantRole", "AccessControl: sender must be an admin to grant", async () => { 
            await wf.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[1]});
        });
        await HlpFail.testFail("wf.grantRole", "AccessControl: sender must be an admin to grant", async () => { 
            await wf.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[2]});
        });

    });

    it('Should all fail to addState with no Schema Admin', async () => {
        let wf = await WorkflowBuilder.deployed();

        //Cannot add state if not authorized
        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([1], {from: accounts[0]});
        });

        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([1], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([1], {from: accounts[9]});
        });
    });

    it('Should grant Schema Admin and addState, until revoked', async () => {
        let wf = await WorkflowBuilder.deployed();

        let WF_SCHEMA_ADMIN_ROLE = await wf.WF_SCHEMA_ADMIN_ROLE();
        await wf.grantRole( WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[9]});
        await wf.addState([1], {from: accounts[1]});

        await wf.revokeRole(WF_SCHEMA_ADMIN_ROLE, accounts[1], {from: accounts[9]});
        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([2], {from: accounts[1]});
        });
    });

    it('Should NOT addState after renouncing role', async () => {
        let wf = await WorkflowBuilder.deployed();

        let WF_SCHEMA_ADMIN_ROLE = await wf.WF_SCHEMA_ADMIN_ROLE();
        await wf.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[5], {from: accounts[9]});
        await wf.addState([2], {from: accounts[5]});

        await wf.renounceRole(WF_SCHEMA_ADMIN_ROLE, accounts[5], {from: accounts[5]});
        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([3], {from: accounts[5]});
        });
    });

    it('Should fail to Add/Finalize', async () => {

        let wf = await WorkflowBuilder.deployed();

        //Cannot add state if not authorized
        //Set account[0] as Schema Admin
        let WF_SCHEMA_ADMIN_ROLE = await wf.WF_SCHEMA_ADMIN_ROLE();
        await wf.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[5], {from: accounts[9]});

        await wf.addState([3], {from: accounts[5]});
        await wf.addState([1,2,3,4], {from: accounts[5]});

        //Cannot finalize without Schema Admin Right
        await HlpFail.testFail("wf.doFinalize", "Unauthorized", async () => { 
            await wf.doFinalize();
        });

        //Cannot finalize incomplete state engine.
        //Cannot finalize without Schema Admin Right
        await HlpFail.testFail("wf.doFinalize", "Edge points to non-existing State", async () => { 
            await wf.doFinalize({from: accounts[5]});
        });

        //Poison the state engine for good by creating an edge to S0
        await wf.addState([0], {from: accounts[5]});

        //Cannot finalize incomplete state engine.
        //Cannot finalize without Schema Admin Right
        await HlpFail.testFail("wf.doFinalize", "Edge cannot point to State Zero", async () => { 
            await wf.doFinalize({from: accounts[5]});
        });

        //Cannot add right when not final
        await HlpFail.testFail("wf.addRight", "Workflow is not final", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.INIT, {from: accounts[5]});
        });

        //Cannot remove right when not final
        await HlpFail.testFail("wf.removeRight", "Workflow is not final", async () => { 
            await wf.removeRight(0, 0, accounts[1], WFRights.INIT, {from: accounts[5]});
        });
    });
});
