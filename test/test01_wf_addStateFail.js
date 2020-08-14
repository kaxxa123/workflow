const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;

contract('Testing Adding States Failures', function (accounts) {

    it('Should fail to Add/Finalize', async () => {

        let wf = await WorkflowBuilder.deployed();

        //Cannot add state if not deployer account
        await HlpFail.testFail("wf.addState", "Unauthorized", async () => { 
            await wf.addState([1], {from: accounts[2]});
        });

        //Add states just fine
        await wf.addState([1]);
        await wf.addState([2]);
        await wf.addState([3]);
        await wf.addState([1,2,3,4]);

        //Cannot finalize if not deployer account
        await HlpFail.testFail("wf.doFinalize", "Unauthorized", async () => { 
            await wf.doFinalize({from: accounts[2]});
        });

        //Cannot finalize incomplete state engine.
        //Cannot finalize if not deployer account
        await HlpFail.testFail("wf.doFinalize", "Edge points to non-existing State", async () => { 
            await wf.doFinalize();
        });

        //Poison the state engine for good by creating an edge to S0
        await wf.addState([0]);

        //Cannot finalize incomplete state engine.
        //Cannot finalize if not deployer account
        await HlpFail.testFail("wf.doFinalize", "Edge cannot point to State Zero", async () => { 
            await wf.doFinalize();
        });

        //Cannot add right when not final
        await HlpFail.testFail("wf.addRight", "Workflow is not final", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.INIT);;
        });

        //Cannot remove right when not final
        await HlpFail.testFail("wf.removeRight", "Workflow is not final", async () => { 
            await wf.removeRight(0, 0, accounts[1], WFRights.INIT);;
        });
    });
});
