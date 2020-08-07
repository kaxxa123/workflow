const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const HlpFail = require('./helpers/testFailure');
// const BigNumber = require("bignumber.js");

contract('Testing Workflow Builer', function (accounts) {

    it('Should Create Workflow OK', async () => {

        let wf = await WorkflowBuilder.deployed();

        const existingState = async (iStartState,iEdge,iEndState) => {
            let next = await wf.states(iStartState,iEdge);
            assert(iEndState == next, "Expected state: " + iEndState + ", Found state: " + next);
        }
    
        const missingState = async (iStartState, iEdge) => {
            await HlpFail.testFail("wf.states", "", async () => { 
                await wf.states(iStartState,iEdge);
            });
        }

        const verifyRight = async (stateid, user, right, iEndState) => {
            let next = await wf.hasRight(stateid, user, right)
            assert(iEndState == next, "Expected state: " + iEndState + ", Found state: " + next);
        }
    
        //Load the State Engine structure
        await wf.addState([1]);
        await wf.addState([2]);
        await wf.addState([3]);
        await wf.addState([1,2,3,4]);
        await wf.addState([]);

        //Lock the State Engine structure
        await wf.doFinalize();

        await wf.addRight(0, 0, accounts[1], 0);
        await wf.addRight(1, 0, accounts[1], 1);
        await wf.addRight(1, 0, accounts[2], 1);
        await wf.addRight(1, 0, accounts[2], 2);

        let tot = await wf.getTotalStates()
        assert(tot == 5, "Expected 5 states");

        //Verify existing states/edges
        await existingState(0,0,1)
        await existingState(1,0,2)
        await existingState(2,0,3)
        await existingState(3,0,1)
        await existingState(3,1,2)
        await existingState(3,2,3)
        await existingState(3,3,4)

        //Verify existing rights
        await verifyRight(0, accounts[1], 0, 1)
        await verifyRight(1, accounts[1], 1, 2)
        await verifyRight(1, accounts[2], 1, 2)
        await verifyRight(1, accounts[2], 2, 2)

        //Verify missing states/edges
        await missingState(0,1)
        await missingState(1,1)
        await missingState(2,1)
        await missingState(3,4)
        await missingState(4,0)
        await missingState(5,0)

        //Verify missing rights
        await verifyRight(1, accounts[3], 2, 0xffffffff)
        await verifyRight(1, accounts[2], 0, 0xffffffff)
        await verifyRight(2, accounts[1], 1, 0xffffffff)
    });

});
