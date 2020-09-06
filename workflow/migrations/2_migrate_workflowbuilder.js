const obj = artifacts.require("WorkflowBuilder");

module.exports = function(deployer) {
  deployer.deploy(obj);
};