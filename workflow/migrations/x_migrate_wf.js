const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const Workflow = artifacts.require("Workflow");
const makeDocSet = require('../test/helpers/wfhelp').makeDocSet;
const WFFlags = require('../test/helpers/wfhelp').WFFlags;

module.exports = function(deployer, network, accounts) {

    deployer.then(async ()  => {
        let builder = await WorkflowBuilder.deployed();
        await deployer.deploy(
            Workflow, 
            builder.address, 
            [makeDocSet(1,1,WFFlags.REQUIRED),
             makeDocSet(1,2,WFFlags.REQUIRED),
             makeDocSet(1,2,0),
             makeDocSet(2,2,0)]);
    })

}
