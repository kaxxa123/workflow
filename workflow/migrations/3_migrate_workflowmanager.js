const obj = artifacts.require("WorkflowManager");

module.exports = function(deployer) {
  deployer.deploy(obj);
};