const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfrights').WFRights;

contract('Testing Rights', function (accounts) {

    it('Should fail to Add/Remove rights 1', async () => {

        let wf = await WorkflowBuilder.deployed();
        //Add states just fine
        await wf.addState([1]);
        await wf.addState([2]);
        await wf.addState([3]);
        await wf.addState([1,2,3,4,5]);
        await wf.addState([]);
        await wf.addState([]);
        await wf.doFinalize();

        //Cannot add states once final
        await HlpFail.testFail("wf.addState", "Workflow is final", async () => { 
            await wf.addState([1]);
        });

        //Cannot add right Unauthorized
        await HlpFail.testFail("wf.addRight", "Unauthorized", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.INIT, {from: accounts[1]});
        });

        //Cannot add right to non-existing state
        await HlpFail.testFail("wf.addRight", "Non-existing state", async () => { 
            await wf.addRight(6, 0, accounts[1], WFRights.APPROVE);
        });

        //Cannot add right to non-existing edge
        await HlpFail.testFail("wf.addRight", "Non-existing edge", async () => { 
            await wf.addRight(0, 1, accounts[1], WFRights.APPROVE);
        });
        await HlpFail.testFail("wf.addRight", "Non-existing edge", async () => { 
            await wf.addRight(4, 0, accounts[1], WFRights.APPROVE);
        });

        //Right not allowed for this edge S0->
        //Only INIT should be allowed
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.APPROVE);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.REVIEW);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.SIGNOFF);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(0, 0, accounts[1], WFRights.ABORT);
        });
        await HlpFail.testFail("wf.addRight", "", async () => { 
            await wf.addRight(0, 0, accounts[1], 100);
        });

        //Right not allowed for this edge S1->S2
        //Only APPROVE should be allowed
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(1, 0, accounts[1], WFRights.INIT);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(1, 0, accounts[1], WFRights.REVIEW);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(1, 0, accounts[1], WFRights.SIGNOFF);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(1, 0, accounts[1], WFRights.ABORT);
        });
        await HlpFail.testFail("wf.addRight", "", async () => { 
            await wf.addRight(1, 0, accounts[1], 100);
        });

        //Right not allowed for this edge S3->S3
        //Only REVIEW should be allowed
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 2, accounts[1], WFRights.INIT);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 2, accounts[1], WFRights.APPROVE);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 2, accounts[1], WFRights.SIGNOFF);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 2, accounts[1], WFRights.ABORT);
        });
        await HlpFail.testFail("wf.addRight", "", async () => { 
            await wf.addRight(3, 2, accounts[1], 100);
        });

        //Right not allowed for this edge S3->S4
        //Only ABORT/SIGNOFF should be allowed
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 3, accounts[1], WFRights.INIT);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 3, accounts[1], WFRights.APPROVE);
        });
        await HlpFail.testFail("wf.addRight", "Right not allowed for this edge", async () => { 
            await wf.addRight(3, 3, accounts[1], WFRights.REVIEW);
        });
        await HlpFail.testFail("wf.addRight", "", async () => { 
            await wf.addRight(3, 3, accounts[1], 100);
        });
    });

    it('Should fail to Add/Remove rights 2', async () => {

        let wf = await WorkflowBuilder.deployed();

        await wf.addRight(0, 0, accounts[1], WFRights.INIT);       //S0 -> S1
        await wf.addRight(1, 0, accounts[1], WFRights.APPROVE);    //S1 -> S2
        await wf.addRight(1, 0, accounts[2], WFRights.APPROVE);    //S1 -> S2
        await wf.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3
        await wf.addRight(3, 0, accounts[1], WFRights.APPROVE);    //S3 -> S1
        await wf.addRight(3, 1, accounts[1], WFRights.APPROVE);    //S3 -> S2
        await wf.addRight(3, 2, accounts[1], WFRights.REVIEW);     //S3 -> S3
        await wf.addRight(3, 3, accounts[1], WFRights.SIGNOFF);    //S3 -> S4
        await wf.addRight(3, 4, accounts[1], WFRights.ABORT);      //S3 -> S5

        //Rights on an edge must be the same
        await HlpFail.testFail("wf.addRight", "Inconsistent rights", async () => { 
            await wf.addRight(3, 4, accounts[2], WFRights.SIGNOFF);    //S3 -> S5
        });

        //Cannot remove right Unauthorized
        await HlpFail.testFail("wf.removeRight", "Unauthorized", async () => { 
            await wf.removeRight(0, 0, accounts[1], WFRights.INIT, {from: accounts[1]});
        });

        //Cannot remove right from non-existing state
        await HlpFail.testFail("wf.removeRight", "Non-existing state", async () => { 
            await wf.removeRight(6, 0, accounts[1], WFRights.APPROVE);
        });

        //Cannot remove right from non-existing edge
        await HlpFail.testFail("wf.removeRight", "Non-existing edge", async () => { 
            await wf.removeRight(0, 1, accounts[1], WFRights.APPROVE);
        });
        await HlpFail.testFail("wf.removeRight", "Non-existing edge", async () => { 
            await wf.removeRight(4, 0, accounts[1], WFRights.APPROVE);
        });

        //Cannot remove non-existing right
        await HlpFail.testFail("wf.removeRight", "User/Right not found", async () => { 
            await wf.removeRight(3, 1, accounts[3], WFRights.APPROVE);
        });
    });

    it('Should Replace Right OK', async () => {

        let wf = await WorkflowBuilder.deployed();

        //Ok to change right with remove+add
        let found = await wf.hasRight(3, 5, accounts[1], WFRights.ABORT)
        assert(found, "Expected right not found");

        found = await wf.hasRight(3, 5, accounts[1], WFRights.SIGNOFF)
        assert(!found, "Unexpected right found");

        await wf.removeRight(3, 4, accounts[1], WFRights.ABORT);        //S3 -> S5
        await wf.addRight(3, 4, accounts[1], WFRights.SIGNOFF);         //S3 -> S5

        found = await wf.hasRight(3, 5, accounts[1], WFRights.ABORT)
        assert(!found, "Unexpected right found");

        found = await wf.hasRight(3, 5, accounts[1], WFRights.SIGNOFF)
        assert(found, "Expected right not found");
    });
});
