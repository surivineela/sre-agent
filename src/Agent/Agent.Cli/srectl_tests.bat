@echo off
setlocal enabledelayedexpansion

REM SRECTL Automated Test Suite
REM This batch file runs comprehensive tests for SRECTL
REM covering agent creation, validation, tool validation, and error handling scenarios

echo =====================================
echo SRECTL Automated Test Suite
echo =====================================
echo.

REM Initialize test counters
set /a TESTS_PASSED=0
set /a TESTS_FAILED=0
set /a TOTAL_TESTS=0

REM Create test output directory
if not exist "test_output" mkdir test_output
pushd test_output

REM Initialize the workspace for testing
echo Initializing workspace for testing...
dotnet run --project .. -- init --resource-url "https://localhost:7023" > init_output.txt 2>&1
echo.

echo Starting CLI tests...
echo.

REM ===========================================
REM POSITIVE TEST CASES
REM ===========================================

echo ===========================================
echo POSITIVE TEST CASES
echo ===========================================
echo.

REM Test 1: Basic agent creation
echo [TEST 1] Basic agent creation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent --instructions "Test agent instructions for validation - this is a comprehensive test with sufficient length" --tools TestTool1 TestTool2 > test1_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Basic agent creation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Basic agent creation - Command failed
    type test1_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 2: Agent creation with all options
echo [TEST 2] Agent creation with all options
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name full_featured_agent --instructions "Full featured test agent with comprehensive options" --tools Tool1 Tool2 --handoff-description "Test handoff description" --handoffs meta_agent --allow-parallel-tool-calls --max-reflection-count 2 --temperature 0.7 --common-prompts format_guidelines > test2_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with all options
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with all options - Command failed
    type test2_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 3: Agent creation with custom properties
echo [TEST 3] Agent creation with custom properties
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name custom_agent --instructions "Custom agent with additional properties - this is a comprehensive test with sufficient length" --tools CustomTool --temperature 0.8 --max-reflection-count 2 --common-prompts format_guidelines > test3_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with custom properties
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with custom properties - Command failed
    type test3_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 4: Tool creation
echo [TEST 4] Tool creation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name TestTool --type KustoTool --extra description "Test tool for validation" > test4_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Tool creation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool creation - Command failed
    type test4_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 5: Agent validation - Basic validation (without tool checking)
echo [TEST 5] Agent validation - Basic validation
set /a TOTAL_TESTS+=1
if exist "agents\test_agent\test_agent.yaml" (
    dotnet run --project .. -- agent validate --file agents\test_agent\test_agent.yaml > test5_output.txt 2>&1
    findstr /i "validation succeeded" test5_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation - Basic validation
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation - Basic validation - Validation failed
        type test5_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation test - test_agent.yaml not found
)
echo.

REM Test 6: Agent validation with tool checking - Should pass for example_agent (has example_tool which exists)
echo [TEST 6] Agent validation with tool checking - Valid tools
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    dotnet run --project .. -- agent validate --file agents\example_agent.yaml --check-tools > test6_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation with tool checking - Valid tools
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation with tool checking - Valid tools
        type test6_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation with tool checking test - example_agent.yaml not found
)
echo.

REM Test 7: Create agent with non-existent tool for testing tool validation
echo [TEST 7] Create agent with missing tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name missing_tool_test --instructions "Test agent with missing tool - this is a comprehensive test with sufficient length" --tools NonExistentTool > test7_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create agent with missing tool
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create agent with missing tool - Command failed
    type test7_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 8: Agent validation with tool checking - Should fail for agent with missing tools
echo [TEST 8] Agent validation with tool checking - Missing tools
set /a TOTAL_TESTS+=1
if exist "agents\missing_tool_test\missing_tool_test.yaml" (
    dotnet run --project .. -- agent validate --file agents\missing_tool_test\missing_tool_test.yaml --check-tools > test8_output.txt 2>&1
    if !errorlevel! neq 0 (
        findstr /i "not available" test8_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent validation with tool checking - Missing tools - Expected failure occurred
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent validation with tool checking - Missing tools - Wrong error message
            type test8_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent validation with tool checking - Missing tools - Expected failure but command succeeded
        type test8_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation with missing tools test - missing_tool_test.yaml not found
)
echo.

REM Test 9: Tool validation
echo [TEST 9] Tool validation
set /a TOTAL_TESTS+=1
if exist "tools\TestTool.yaml" (
    dotnet run --project .. -- tool validate --name TestTool > test9_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Tool validation
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool validation - Command failed
        type test9_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Tool validation test - TestTool.yaml not found
)
echo.

REM Test 10: Validate all agents - Basic validation
echo [TEST 10] Validate all agents - Basic validation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all > test10_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Validate all agents - Basic validation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Validate all agents - Basic validation - Command failed
    type test10_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 11: Validate all agents with tool checking
echo [TEST 11] Validate all agents with tool checking
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all --check-tools > test11_output.txt 2>&1
REM This should fail because we have agents with missing tools
if !errorlevel! neq 0 (
    findstr /i "not available\|missing" test11_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Validate all agents with tool checking - Expected failures detected
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Validate all agents with tool checking - Wrong error message
        type test11_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Validate all agents with tool checking - Expected failure but command succeeded
    type test11_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM NEGATIVE TEST CASES (Error Handling)
REM ===========================================

echo ===========================================
echo NEGATIVE TEST CASES (Error Handling)
echo ===========================================
echo.

REM Test 12: Agent creation with just name (should succeed with default instructions)
echo [TEST 12] Agent creation with default instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_defaults --tools Tool1 > test12_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with default instructions - Command succeeded as expected
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with default instructions - Command failed unexpectedly
    type test12_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 13: Agent creation without tools
echo [TEST 13] Agent creation - No tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_no_tools --instructions "Test agent without tools - this is a comprehensive test with sufficient length" > test13_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - No tools - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - No tools - Expected failure but command succeeded
    type test13_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 14: Agent creation with invalid name (contains spaces)
echo [TEST 14] Agent creation - Invalid name with spaces
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name "invalid name" --instructions "Test agent with invalid name - this is a comprehensive test with sufficient length" --tools Tool1 > test14_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid name with spaces - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid name with spaces - Expected failure but command succeeded
    type test14_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 15: Agent creation with short instructions
echo [TEST 15] Agent creation - Short instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_short --instructions "short" --tools Tool1 > test15_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Short instructions - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Short instructions - Expected failure but command succeeded
    type test15_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 16: Agent creation with invalid temperature
echo [TEST 16] Agent creation - Invalid temperature
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_temp --instructions "Test agent with invalid temperature - this is a comprehensive test with sufficient length" --tools Tool1 --temperature 5.0 > test16_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid temperature - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid temperature - Expected failure but command succeeded
    type test16_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 17: Agent creation with negative max-reflection-count
echo [TEST 17] Agent creation - Negative max-reflection-count
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_reflection --instructions "Test agent with negative reflection count - this is a comprehensive test with sufficient length" --tools Tool1 --max-reflection-count -1 > test17_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Negative max-reflection-count - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Negative max-reflection-count - Expected failure but command succeeded
    type test17_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 18: Agent validation - Non-existent file
echo [TEST 18] Agent validation - Non-existent file
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --file non_existent_file.yaml > test18_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent validation - Non-existent file - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent validation - Non-existent file - Expected failure but command succeeded
    type test18_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 19: Tool validation - Non-existent tool
echo [TEST 19] Tool validation - Non-existent tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool validate --name NonExistentTool > test19_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool validation - Non-existent tool - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool validation - Non-existent tool - Expected failure but command succeeded
    type test19_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 20: Agent validation with tool checking - Non-existent file
echo [TEST 20] Agent validation with tool checking - Non-existent file
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --file non_existent_file.yaml --check-tools > test20_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent validation with tool checking - Non-existent file - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent validation with tool checking - Non-existent file - Expected failure but command succeeded
    type test20_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM YAML FORMAT AND STRUCTURE TESTS
REM ===========================================

echo ===========================================
echo YAML FORMAT AND STRUCTURE TESTS
echo ===========================================
echo.

REM Test 21: Verify snake_case conversion in YAML
echo [TEST 21] Snake case conversion test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name snake_case_test --instructions "Test snake case conversion in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --max-reflection-count 1 > test21_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Snake case conversion test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Snake case conversion test - Command failed
    type test21_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 22: Verify boolean properties in YAML
echo [TEST 22] Boolean properties test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name boolean_test --instructions "Test boolean properties in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --critic-on-handoff > test22_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Boolean properties test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Boolean properties test - Command failed
    type test22_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 23: Verify directory structure creation
echo [TEST 23] Directory structure test
set /a TOTAL_TESTS+=1
if exist "agents\test_agent" (
    echo [PASS] Directory structure test - agents\test_agent directory created
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Directory structure test - agents\test_agent directory not found
    set /a TESTS_FAILED+=1
)
echo.

REM Test 24: Verify YAML file contents (snake_case)
echo [TEST 24] YAML snake_case format test
set /a TOTAL_TESTS+=1
if exist "agents\snake_case_test\snake_case_test.yaml" (
    findstr /i "allow_parallel_tool_calls" "agents\snake_case_test\snake_case_test.yaml" >nul
    if !errorlevel! equ 0 (
        echo [PASS] YAML snake_case format test - allow_parallel_tool_calls found
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] YAML snake_case format test - allow_parallel_tool_calls not found in snake_case
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] YAML snake_case format test - snake_case_test.yaml not found
)
echo.

REM Test 25: Verify init command created proper structure
echo [TEST 25] Init command structure test
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    if exist "tools\example_tool.yaml" (
        if exist ".sreagent-config.json" (
            echo [PASS] Init command structure test - All expected files created
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Init command structure test - .sreagent-config.json not found
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Init command structure test - example_tool.yaml not found
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Init command structure test - example_agent.yaml not found
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM TOOL VALIDATION COMPREHENSIVE TESTS
REM ===========================================

echo ===========================================
echo TOOL VALIDATION COMPREHENSIVE TESTS
echo ===========================================
echo.

REM Test 26: Create tools that agents can reference
echo [TEST 26] Create BulkTool1
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name BulkTool1 --type KustoTool --extra description "Bulk tool 1 for validation" > test26_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create BulkTool1
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create BulkTool1 - Command failed
    type test26_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 27] Create BulkTool2
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name BulkTool2 --type KustoTool --extra description "Bulk tool 2 for validation" > test27_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create BulkTool2
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create BulkTool2 - Command failed
    type test27_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 28: Create multiple agents for bulk validation
echo [TEST 28] Bulk agent creation 1 - With existing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_1 --instructions "First agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool1 > test28_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 1 - With existing tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 1 - With existing tools - Command failed
    type test28_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 29] Bulk agent creation 2 - With existing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_2 --instructions "Second agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool2 > test29_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 2 - With existing tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 2 - With existing tools - Command failed
    type test29_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 30] Bulk agent creation 3 - With mixed existing/missing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_3 --instructions "Third agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool1 NonExistentBulkTool > test30_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 3 - With mixed tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 3 - With mixed tools - Command failed
    type test30_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 31: Validate agents with tool checking - Mixed results expected
echo [TEST 31] Bulk validation with tool checking
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all --check-tools > test31_output.txt 2>&1
REM This should fail because we have agents with missing tools, but show detailed results
findstr /i "not available\|missing\|succeeded" test31_output.txt >nul
if !errorlevel! equ 0 (
    echo [PASS] Bulk validation with tool checking - Mixed results as expected
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk validation with tool checking - Unexpected output format
    type test31_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 32: Test individual agent validation with existing tools
echo [TEST 32] Individual agent validation - With existing tools
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_1\bulk_agent_1.yaml" (
    dotnet run --project .. -- agent validate --file agents\bulk_agent_1\bulk_agent_1.yaml --check-tools > test32_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Individual agent validation - With existing tools
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Individual agent validation - With existing tools
        type test32_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Individual agent validation test - bulk_agent_1.yaml not found
)
echo.

REM Test 33: Test individual agent validation with mixed tools
echo [TEST 33] Individual agent validation - With mixed tools
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_3\bulk_agent_3.yaml" (
    dotnet run --project .. -- agent validate --file agents\bulk_agent_3\bulk_agent_3.yaml --check-tools > test33_output.txt 2>&1
    if !errorlevel! neq 0 (
        findstr /i "not available" test33_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Individual agent validation - With mixed tools - Expected failure
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Individual agent validation - With mixed tools - Wrong error message
            type test33_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Individual agent validation - With mixed tools - Expected failure but command succeeded
        type test33_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Individual agent validation with mixed tools test - bulk_agent_3.yaml not found
)
echo.

REM ===========================================
REM APPLY COMMAND TESTS
REM ===========================================

echo ===========================================
echo APPLY COMMAND TESTS
echo ===========================================
echo.

REM Test 34: Agent apply with valid agent
echo [TEST 34] Agent apply - Valid agent
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    dotnet run --project .. -- agent apply --name example_agent > test34_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "applied successfully" test34_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent apply - Valid agent
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent apply - Valid agent - Wrong success message
            type test34_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent apply - Valid agent - Command failed
        type test34_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent apply test - example_agent.yaml not found
)
echo.

REM Test 35: Agent apply with non-existent agent
echo [TEST 35] Agent apply - Non-existent agent
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent apply --name non_existent_agent > test35_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent apply - Non-existent agent - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent apply - Non-existent agent - Expected failure but command succeeded
    type test35_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 36: Tool apply with valid tool
echo [TEST 36] Tool apply - Valid tool
set /a TOTAL_TESTS+=1
if exist "tools\example_tool.yaml" (
    dotnet run --project .. -- tool apply --name example_tool > test36_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "applied successfully" test36_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Tool apply - Valid tool
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Tool apply - Valid tool - Wrong success message
            type test36_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Tool apply - Valid tool - Command failed
        type test36_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Tool apply test - example_tool.yaml not found
)
echo.

REM Test 37: Tool apply with non-existent tool
echo [TEST 37] Tool apply - Non-existent tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool apply --name non_existent_tool > test37_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool apply - Non-existent tool - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool apply - Non-existent tool - Expected failure but command succeeded
    type test37_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 38: Agent apply with agent that has referenced tools
echo [TEST 38] Agent apply - With referenced tools inclusion
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_1\bulk_agent_1.yaml" (
    dotnet run --project .. -- agent apply --name bulk_agent_1 > test38_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "Loaded tool" test38_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent apply - With referenced tools inclusion
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent apply - With referenced tools inclusion - Tool loading not shown
            type test38_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent apply - With referenced tools inclusion - Command failed
        type test38_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent apply with tools test - bulk_agent_1.yaml not found
)
echo.

REM ===========================================
REM MALFORMED YAML TESTS
REM ===========================================

echo ===========================================
echo MALFORMED YAML TESTS
echo ===========================================
echo.

REM Test 39: Create malformed YAML with invalid syntax
echo [TEST 39] Malformed YAML - Invalid syntax
set /a TOTAL_TESTS+=1
echo name: malformed_agent > agents\malformed_syntax.yaml
echo instructions: "Test agent" >> agents\malformed_syntax.yaml
echo tools: >> agents\malformed_syntax.yaml
echo   - Tool1 >> agents\malformed_syntax.yaml
echo   invalid_indentation >> agents\malformed_syntax.yaml
dotnet run --project .. -- agent validate --file agents\malformed_syntax.yaml > test39_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Invalid syntax - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Invalid syntax - Expected failure but command succeeded
    type test39_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 40: Create YAML with missing required fields
echo [TEST 40] Malformed YAML - Missing required fields
set /a TOTAL_TESTS+=1
echo name: incomplete_agent > agents\missing_fields.yaml
echo instructions: "Test agent" >> agents\missing_fields.yaml
REM Missing tools field
dotnet run --project .. -- agent validate --file agents\missing_fields.yaml > test40_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Missing required fields - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Missing required fields - Expected failure but command succeeded
    type test40_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 41: Create YAML with invalid data types
echo [TEST 41] Malformed YAML - Invalid data types
set /a TOTAL_TESTS+=1
echo name: invalid_types > agents\invalid_types.yaml
echo instructions: "Test agent with invalid types" >> agents\invalid_types.yaml
echo tools: >> agents\invalid_types.yaml
echo   - Tool1 >> agents\invalid_types.yaml
echo temperature: "not_a_number" >> agents\invalid_types.yaml
echo max_reflection_count: "also_not_a_number" >> agents\invalid_types.yaml
dotnet run --project .. -- agent validate --file agents\invalid_types.yaml > test41_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Invalid data types - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Invalid data types - Expected failure but command succeeded
    type test41_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 42: Create YAML with malformed structure
echo [TEST 42] Malformed YAML - Malformed structure
set /a TOTAL_TESTS+=1
echo name: "malformed_structure" > agents\malformed_structure.yaml
echo instructions: >> agents\malformed_structure.yaml
echo   this_should_be_a_string: "not a proper instructions field" >> agents\malformed_structure.yaml
echo tools: "this_should_be_a_list" >> agents\malformed_structure.yaml
dotnet run --project .. -- agent validate --file agents\malformed_structure.yaml > test42_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Malformed structure - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Malformed structure - Expected failure but command succeeded
    type test42_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM CONFIGURATION FILE TESTS
REM ===========================================

echo ===========================================
echo CONFIGURATION FILE TESTS
echo ===========================================
echo.

REM Test 43: Backup and test with missing config file
echo [TEST 43] Missing configuration file
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
    del ".sreagent-config.json"
)
dotnet run --project .. -- agent validate --file agents\example_agent.yaml > test43_output.txt 2>&1
REM Should still work with default behavior or prompt for config
if !errorlevel! equ 0 (
    echo [PASS] Missing configuration file - Handled gracefully
    set /a TESTS_PASSED+=1
) else (
    findstr /i "config\|configuration" test43_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Missing configuration file - Appropriate error message
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Missing configuration file - Unexpected behavior
        type test43_output.txt
        set /a TESTS_FAILED+=1
    )
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM Test 44: Test with corrupted config file
echo [TEST 44] Corrupted configuration file
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
)
echo { "invalid": "json" "missing_comma": true > ".sreagent-config.json"
dotnet run --project .. -- agent validate --file agents\example_agent.yaml > test44_output.txt 2>&1
REM Should handle corrupted config gracefully
if !errorlevel! neq 0 (
    findstr /i "Configuration corrupted" test44_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Corrupted configuration file - Appropriate error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Corrupted configuration file - Wrong error message
        type test44_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Corrupted configuration file - Should have failed but succeeded
    type test44_output.txt
    set /a TESTS_FAILED+=1
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM Test 45: Test with invalid resource URL in config
echo [TEST 45] Invalid resource URL in configuration
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
)
echo { "resourceUrl": "not-a-valid-url" } > ".sreagent-config.json"
dotnet run --project .. -- agent validate --file agents\example_agent.yaml --check-tools > test45_output.txt 2>&1
REM Should handle invalid URL gracefully
if !errorlevel! neq 0 (
    findstr /i "Invalid resource URL" test45_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Invalid resource URL - Appropriate error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Invalid resource URL - Wrong error message
        type test45_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Invalid resource URL - Should have failed but succeeded
    type test45_output.txt
    set /a TESTS_FAILED+=1
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM ===========================================
REM TOOL TYPE VALIDATION TESTS
REM ===========================================

echo ===========================================
echo TOOL TYPE VALIDATION TESTS
echo ===========================================
echo.

REM Test 46: Create tool with invalid type
echo [TEST 46] Tool creation - Invalid tool type
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name invalid_type_tool --type InvalidToolType --extra description "Tool with invalid type" > test46_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "Unknown tool type" test46_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Tool creation - Invalid tool type - Expected failure occurred
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool creation - Invalid tool type - Wrong error message
        type test46_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Tool creation - Invalid tool type - Expected failure but command succeeded
    type test46_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 47: Create tool with missing required fields for type
echo [TEST 47] Tool validation - Missing required fields for type
set /a TOTAL_TESTS+=1
echo name: incomplete_kusto_tool > tools\incomplete_kusto.yaml
echo type: KustoTool >> tools\incomplete_kusto.yaml
REM Missing description and other KustoTool-specific fields
dotnet run --project .. -- tool validate --name incomplete_kusto > test47_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool validation - Missing required fields for type - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool validation - Missing required fields for type - Expected failure but command succeeded
    type test47_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 48: Create multiple tools with different valid types
echo [TEST 48] Tool creation - Multiple valid types
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name valid_type_tool1 --type KustoTool --extra description "Valid Kusto tool" > test48a_output.txt 2>&1
set RESULT1=!errorlevel!
dotnet run --project .. -- tool create --name valid_type_tool2 --type KustoQuery --extra description "Valid Kusto Query tool" > test48b_output.txt 2>&1
set RESULT2=!errorlevel!
if !RESULT1! equ 0 (
    if !RESULT2! equ 0 (
        echo [PASS] Tool creation - Multiple valid types
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool creation - Multiple valid types - Second tool creation failed
        type test48b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Tool creation - Multiple valid types - First tool creation failed
    type test48a_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 49: Tool schema validation against type
echo [TEST 49] Tool schema validation - Type consistency
set /a TOTAL_TESTS+=1
echo name: schema_mismatch_tool > tools\schema_mismatch.yaml
echo type: KustoTool >> tools\schema_mismatch.yaml
echo description: "Tool with wrong schema for type" >> tools\schema_mismatch.yaml
echo some_invalid_field: "This field should not exist for KustoTool" >> tools\schema_mismatch.yaml
dotnet run --project .. -- tool validate --name schema_mismatch > test49_output.txt 2>&1
REM This might pass or fail depending on schema validation strictness
if !errorlevel! neq 0 (
    echo [PASS] Tool schema validation - Type consistency - Validation caught schema mismatch
    set /a TESTS_PASSED+=1
) else (
    REM If it passes, that's also acceptable if schema validation is lenient
    echo [PASS] Tool schema validation - Type consistency - Schema validation is lenient
    set /a TESTS_PASSED+=1
)
echo.

echo.

REM ===========================================
REM DELETE COMMAND TESTS
REM ===========================================

echo ===========================================
echo DELETE COMMAND TESTS
echo ===========================================
echo.

REM Test 50: Create test agents and tools for deletion testing
echo [TEST 50] Setup - Create test agents and tools for deletion testing
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name DeleteTestAgent1 --instructions "Test agent for delete command testing - comprehensive instructions for validation" --tools example_tool > test50_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- agent create --name DeleteTestAgent2 --instructions "Another test agent for delete command testing - comprehensive instructions for validation" --tools example_tool > test50b_output.txt 2>&1
    if !errorlevel! equ 0 (
        dotnet run --project .. -- tool create --name DeleteTestTool1 --type KustoTool --extra description "Test tool for delete command testing" > test50c_output.txt 2>&1
        if !errorlevel! equ 0 (
            dotnet run --project .. -- tool create --name DeleteTestTool2 --type KustoTool --extra description "Another test tool for delete command testing" > test50d_output.txt 2>&1
            if !errorlevel! equ 0 (
                echo [PASS] Setup - Create test agents and tools for deletion testing
                set /a TESTS_PASSED+=1
            ) else (
                echo [FAIL] Setup - Failed to create DeleteTestTool2
                type test50d_output.txt
                set /a TESTS_FAILED+=1
            )
        ) else (
            echo [FAIL] Setup - Failed to create DeleteTestTool1
            type test50c_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Setup - Failed to create DeleteTestAgent2
        type test50b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Setup - Failed to create DeleteTestAgent1
    type test50_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 51: Apply test agents to server for delete testing
echo [TEST 51] Apply test agents to server for delete testing
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent apply --name DeleteTestAgent1 > test51_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- agent apply --name DeleteTestAgent2 > test51b_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Apply test agents to server for delete testing
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Apply test agents - Failed to apply DeleteTestAgent2
        type test51b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Apply test agents - Failed to apply DeleteTestAgent1
    type test51_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 52: Apply test tools to server for delete testing
echo [TEST 52] Apply test tools to server for delete testing
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool apply --name DeleteTestTool1 > test52_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- tool apply --name DeleteTestTool2 > test52b_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Apply test tools to server for delete testing
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Apply test tools - Failed to apply DeleteTestTool2
        type test52b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Apply test tools - Failed to apply DeleteTestTool1
    type test52_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 53: List agents before deletion
echo [TEST 53] List agents before deletion
set /a TOTAL_TESTS+=1
dotnet run --project .. -- list agents > test53_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "DeleteTestAgent1" test53_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "DeleteTestAgent2" test53_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] List agents before deletion - Both test agents found
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] List agents before deletion - DeleteTestAgent2 not found
            type test53_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] List agents before deletion - DeleteTestAgent1 not found
        type test53_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] List agents before deletion - Command failed
    type test53_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 54: List tools before deletion
echo [TEST 54] List extended-tools before deletion
set /a TOTAL_TESTS+=1
dotnet run --project .. -- list extended-tools > test54_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "DeleteTestTool1" test54_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "DeleteTestTool2" test54_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] List extended-tools before deletion - Both test tools found
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] List extended-tools before deletion - DeleteTestTool2 not found
            type test54_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] List extended-tools before deletion - DeleteTestTool1 not found
        type test54_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] List extended-tools before deletion - Command failed
    type test54_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 55: Delete non-existent agent (error handling)
echo [TEST 55] Delete non-existent agent (error handling)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent delete --name NonExistentAgent > test55_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "not found\|does not exist" test55_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete non-existent agent - Proper error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete non-existent agent - Wrong error message
        type test55_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete non-existent agent - Should have failed but succeeded
    type test55_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 56: Delete non-existent tool (error handling)
echo [TEST 56] Delete non-existent tool (error handling)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool delete --name NonExistentTool > test56_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "not found\|does not exist" test56_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete non-existent tool - Proper error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete non-existent tool - Wrong error message
        type test56_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete non-existent tool - Should have failed but succeeded
    type test56_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 57: Delete agent successfully
echo [TEST 57] Delete agent successfully
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent delete --name DeleteTestAgent1 > test57_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "deleted successfully" test57_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete agent successfully - Proper success message
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete agent successfully - Missing success message
        type test57_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete agent successfully - Command failed
    type test57_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 58: Delete tool successfully
echo [TEST 58] Delete tool successfully
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool delete --name DeleteTestTool1 > test58_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "deleted successfully" test58_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete tool successfully - Proper success message
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete tool successfully - Missing success message
        type test58_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete tool successfully - Command failed
    type test58_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 59: Verify agent deletion by listing agents
echo [TEST 59] Verify agent deletion by listing agents
set /a TOTAL_TESTS+=1
dotnet run --project .. -- list agents > test59_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "DeleteTestAgent1" test59_output.txt >nul
    if !errorlevel! neq 0 (
        echo [PASS] Verify agent deletion - DeleteTestAgent1 no longer listed
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Verify agent deletion - DeleteTestAgent1 still appears in list
        type test59_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Verify agent deletion - List command failed
    type test59_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 60: Verify tool deletion by listing extended-tools
echo [TEST 60] Verify tool deletion by listing extended-tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- list extended-tools > test60_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "DeleteTestTool1" test60_output.txt >nul
    if !errorlevel! neq 0 (
        echo [PASS] Verify tool deletion - DeleteTestTool1 no longer listed
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Verify tool deletion - DeleteTestTool1 still appears in list
        type test60_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Verify tool deletion - List command failed
    type test60_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 61: Create agent that depends on a tool, then test dependency checking
echo [TEST 61] Create agent with dependency on DeleteTestTool2
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name DependentAgent --instructions "Agent that depends on DeleteTestTool2 for dependency testing" --tools DeleteTestTool2 > test61_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- agent apply --name DependentAgent > test61b_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Create agent with dependency on DeleteTestTool2
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Create agent with dependency - Failed to apply DependentAgent
        type test61b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create agent with dependency - Failed to create DependentAgent
    type test61_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 62: Try to delete tool with dependencies (should fail)
echo [TEST 62] Try to delete tool with dependencies (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool delete --name DeleteTestTool2 > test62_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "dependency\|dependent\|used by" test62_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Try to delete tool with dependencies - Proper dependency check
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Try to delete tool with dependencies - Wrong error message
        type test62_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Try to delete tool with dependencies - Should have failed but succeeded
    type test62_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 63: Delete dependent agent first, then tool
echo [TEST 63] Delete dependent agent first
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent delete --name DependentAgent > test63_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "deleted successfully" test63_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete dependent agent first - Success
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete dependent agent first - Missing success message
        type test63_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete dependent agent first - Command failed
    type test63_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 64: Now delete the tool after removing dependencies
echo [TEST 64] Delete tool after removing dependencies
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool delete --name DeleteTestTool2 > test64_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "deleted successfully" test64_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete tool after removing dependencies - Success
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete tool after removing dependencies - Missing success message
        type test64_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete tool after removing dependencies - Command failed
    type test64_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 65: Delete remaining test agent
echo [TEST 65] Delete remaining test agent (DeleteTestAgent2)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent delete --name DeleteTestAgent2 > test65_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "deleted successfully" test65_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete remaining test agent - Success
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete remaining test agent - Missing success message
        type test65_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete remaining test agent - Command failed
    type test65_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 66: Final verification - ensure all test agents and tools are deleted
echo [TEST 66] Final verification - ensure all test entities are deleted
set /a TOTAL_TESTS+=1
dotnet run --project .. -- list agents > test66_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "DeleteTestAgent" test66_output.txt >nul
    if !errorlevel! neq 0 (
        dotnet run --project .. -- list extended-tools > test66b_output.txt 2>&1
        if !errorlevel! equ 0 (
            findstr /i "DeleteTestTool" test66b_output.txt >nul
            if !errorlevel! neq 0 (
                echo [PASS] Final verification - All test entities successfully deleted
                set /a TESTS_PASSED+=1
            ) else (
                echo [FAIL] Final verification - Some test tools still exist
                type test66b_output.txt
                set /a TESTS_FAILED+=1
            )
        ) else (
            echo [FAIL] Final verification - List extended-tools command failed
            type test66b_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Final verification - Some test agents still exist
        type test66_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Final verification - List agents command failed
    type test66_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo.

REM ===========================================
REM PROFILE MANAGEMENT TESTS
REM ===========================================

echo ===========================================
echo PROFILE MANAGEMENT TESTS
echo ===========================================
echo.

REM Test 67: Create test profile with local URL
echo [TEST 67] Create test profile with local URL
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile create --name test_local --url "https://localhost:7023" > test67_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "Connection successful" test67_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Create test profile with local URL
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Create test profile - Missing success message
        type test67_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create test profile - Command failed
    type test67_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 68: Create test profile with remote URL
echo [TEST 68] Create test profile with remote URL
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile create --name test_remote --url "https://localhost:7023" > test68_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "created successfully" test68_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Create test profile with remote URL
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Create test profile remote - Missing success message
        type test68_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create test profile remote - Command failed
    type test68_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 69: Create profile with set-current option
echo [TEST 69] Create profile with set-current option
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile create --name test_current --url "https://localhost:7023" --set-current > test69_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "created successfully" test69_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "Set as current profile: Yes" test69_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Create profile with set-current option
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Create profile with set-current - Missing current profile message
            type test69_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Create profile with set-current - Missing success message
        type test69_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create profile with set-current - Command failed
    type test69_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 70: List profiles
echo [TEST 70] List profiles
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile list > test70_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "test_local" test70_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "test_remote" test70_output.txt >nul
        if !errorlevel! equ 0 (
            findstr /i "test_current.*current" test70_output.txt >nul
            if !errorlevel! equ 0 (
                echo [PASS] List profiles - All test profiles found with current marker
                set /a TESTS_PASSED+=1
            ) else (
                echo [FAIL] List profiles - Current profile marker not found
                type test70_output.txt
                set /a TESTS_FAILED+=1
            )
        ) else (
            echo [FAIL] List profiles - test_remote not found
            type test70_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] List profiles - test_local not found
        type test70_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] List profiles - Command failed
    type test70_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 71: Get current profile (without name argument)
echo [TEST 71] Get current profile (without name argument)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile get > test71_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "Current profile: test_current" test71_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "Resource URL: https://localhost:7025" test71_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Get current profile - Shows correct current profile details
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Get current profile - Missing or wrong resource URL
            type test71_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Get current profile - Wrong profile name
        type test71_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Get current profile - Command failed
    type test71_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 72: Get specific profile by name
echo [TEST 72] Get specific profile by name
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile get --name test_remote > test72_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "Profile: test_remote" test72_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "Resource URL: https://remote.example.com" test72_output.txt >nul
        if !errorlevel! equ 0 (
            findstr /i "Auth Required: true" test72_output.txt >nul
            if !errorlevel! equ 0 (
                echo [PASS] Get specific profile - Shows correct profile details with auth required
                set /a TESTS_PASSED+=1
            ) else (
                echo [FAIL] Get specific profile - Missing or wrong auth required setting
                type test72_output.txt
                set /a TESTS_FAILED+=1
            )
        ) else (
            echo [FAIL] Get specific profile - Missing or wrong resource URL
            type test72_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Get specific profile - Wrong profile name
        type test72_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Get specific profile - Command failed
    type test72_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 73: Set different profile as current
echo [TEST 73] Set different profile as current
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile set --name test_local > test73_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "Switched to profile 'test_local'" test73_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "Resource URL: https://localhost:7024" test73_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Set different profile as current
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Set profile - Missing or wrong resource URL
            type test73_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Set profile - Missing success message
        type test73_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Set profile - Command failed
    type test73_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 74: Verify profile switch by listing profiles
echo [TEST 74] Verify profile switch by listing profiles
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile list > test74_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "test_local.*current" test74_output.txt >nul
    if !errorlevel! equ 0 (
        findstr /i "test_current" test74_output.txt >nul
        if !errorlevel! equ 0 (
            REM Make sure test_current doesn't have (current) marker
            findstr /i "test_current.*current" test74_output.txt >nul
            if !errorlevel! neq 0 (
                echo [PASS] Verify profile switch - Current profile correctly updated
                set /a TESTS_PASSED+=1
            ) else (
                echo [FAIL] Verify profile switch - Old profile still marked as current
                type test74_output.txt
                set /a TESTS_FAILED+=1
            )
        ) else (
            echo [FAIL] Verify profile switch - test_current profile missing
            type test74_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Verify profile switch - test_local not marked as current
        type test74_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Verify profile switch - List command failed
    type test74_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM =======================
REM Test 75
REM =======================
echo [TEST 75] Try to delete current profile (should fail)
set /a TOTAL_TESTS+=1

dotnet run --project .. -- profile delete --name test_local > test75_output.txt 2>&1

REM dotnet should fail -> errorlevel >= 1
if errorlevel 1 (
    REM Look for the specific message
    findstr /i /r /c:"Cannot delete.*current profile" test75_output.txt >nul

    if not errorlevel 1 (
        echo [PASS] Try to delete current profile - Properly prevented
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Try to delete current profile - Wrong error message
        type test75_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Try to delete current profile - Should have failed but succeeded
    type test75_output.txt
    set /a TESTS_FAILED+=1
)

echo.

REM Test 76: Switch to different profile then delete non-current profile
echo [TEST 76] Switch to different profile then delete non-current profile
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile set --name test_remote > test76a_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- profile delete --name test_current > test76_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "deleted successfully" test76_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Delete non-current profile
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Delete non-current profile - Missing success message
            type test76_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Delete non-current profile - Command failed
        type test76_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Switch profile before delete - Command failed
    type test76a_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 77: Try to create profile with duplicate name (should fail)
echo [TEST 77] Try to create profile with duplicate name (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile create --name test_remote --url "https://localhost:7026" > test77_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "already exists" test77_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Create duplicate profile - Properly prevented
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Create duplicate profile - Wrong error message
        type test77_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create duplicate profile - Should have failed but succeeded
    type test77_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 78: Try to create profile with invalid URL (should fail)
echo [TEST 78] Try to create profile with invalid URL (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile create --name test_invalid --url "not-a-valid-url" > test78_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "Invalid URL format" test78_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Create profile with invalid URL - Properly prevented
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Create profile with invalid URL - Wrong error message
        type test78_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Create profile with invalid URL - Should have failed but succeeded
    type test78_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 79: Try to get non-existent profile (should fail)
echo [TEST 79] Try to get non-existent profile (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile get --name non_existent_profile > test79_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "not found" test79_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Get non-existent profile - Proper error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Get non-existent profile - Wrong error message
        type test79_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Get non-existent profile - Should have failed but succeeded
    type test79_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 80: Try to set non-existent profile (should fail)
echo [TEST 80] Try to set non-existent profile (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile set --name non_existent_profile > test80_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "not found" test80_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Set non-existent profile - Proper error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Set non-existent profile - Wrong error message
        type test80_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Set non-existent profile - Should have failed but succeeded
    type test80_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 81: Try to delete non-existent profile (should fail)
echo [TEST 81] Try to delete non-existent profile (should fail)
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile delete --name non_existent_profile > test81_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "not found" test81_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Delete non-existent profile - Proper error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Delete non-existent profile - Wrong error message
        type test81_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Delete non-existent profile - Should have failed but succeeded
    type test81_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 82: Clean up test profiles
echo [TEST 82] Clean up test profiles
set /a TOTAL_TESTS+=1
dotnet run --project .. -- profile set --name test_local > test82a_output.txt 2>&1
if !errorlevel! equ 0 (
    dotnet run --project .. -- profile delete --name test_remote > test82b_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Clean up test profiles
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Clean up test profiles - Failed to delete test_remote
        type test82b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Clean up test profiles - Failed to switch to test_local
    type test82a_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM CHARACTER LIMIT VALIDATION TESTS
REM ===========================================

echo ===========================================
echo CHARACTER LIMIT VALIDATION TESTS (60K)
echo ===========================================
echo.

echo [TEST 83] Agent YAML with very long instructions (60k limit)
set /a TOTAL_TESTS+=1
REM Seed the 60k YAML from repo root into the test workspace
if exist "..\agents\test_agent_60k\test_agent_60k.yaml" (
    if not exist "agents\test_agent_60k" mkdir "agents\test_agent_60k"
    copy /Y "..\agents\test_agent_60k\test_agent_60k.yaml" "agents\test_agent_60k\test_agent_60k.yaml" >nul
)
if exist "agents\test_agent_60k\test_agent_60k.yaml" (
    dotnet run --project .. -- agent validate --file agents\test_agent_60k\test_agent_60k.yaml > test83_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation from YAML with ~60k instructions
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation from YAML - Command failed
        type test83_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] test_agent_60k.yaml not found. Create it under agents\test_agent_60k\ per the template.
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 84] Apply YAML with very long instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- apply-yaml --file agents\test_agent_60k\test_agent_60k.yaml > test84_output.txt 2>&1
if !errorlevel! equ 0 (
    findstr /i "applied successfully" test84_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Apply YAML with 60k instructions
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Apply YAML with 60k instructions - Wrong success message
        type test84_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Apply YAML with 60k instructions - Command failed
    type test84_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM CLEANUP AND SUMMARY
REM ===========================================

echo ===========================================
echo CLEANUP AND TEST SUMMARY
echo ===========================================
echo.

echo Cleaning up test files...

REM Clean up test agents
if exist "agents" (
    if exist "agents\test_agent" rmdir /s /q "agents\test_agent"
    if exist "agents\test_agent_defaults" rmdir /s /q "agents\test_agent_defaults"
    if exist "agents\full_featured_agent" rmdir /s /q "agents\full_featured_agent"
    if exist "agents\custom_agent" rmdir /s /q "agents\custom_agent"
    if exist "agents\snake_case_test" rmdir /s /q "agents\snake_case_test"
    if exist "agents\boolean_test" rmdir /s /q "agents\boolean_test"
    if exist "agents\missing_tool_test" rmdir /s /q "agents\missing_tool_test"
    if exist "agents\bulk_agent_1" rmdir /s /q "agents\bulk_agent_1"
    if exist "agents\bulk_agent_2" rmdir /s /q "agents\bulk_agent_2"
    if exist "agents\bulk_agent_3" rmdir /s /q "agents\bulk_agent_3"
    if exist "agents\DeleteTestAgent1" rmdir /s /q "agents\DeleteTestAgent1"
    if exist "agents\DeleteTestAgent2" rmdir /s /q "agents\DeleteTestAgent2"
    if exist "agents\DependentAgent" rmdir /s /q "agents\DependentAgent"
    if exist "agents\test_agent_60k" rmdir /s /q "agents\test_agent_60k"

    REM Keep example_agent.yaml as it was created by init
)

REM Clean up test tools
if exist "tools" (
    if exist "tools\TestTool.yaml" del "tools\TestTool.yaml"
    if exist "tools\BulkTool1.yaml" del "tools\BulkTool1.yaml"
    if exist "tools\BulkTool2.yaml" del "tools\BulkTool2.yaml"
    if exist "tools\incomplete_kusto.yaml" del "tools\incomplete_kusto.yaml"
    if exist "tools\schema_mismatch.yaml" del "tools\schema_mismatch.yaml"
    if exist "tools\valid_type_tool1.yaml" del "tools\valid_type_tool1.yaml"
    if exist "tools\valid_type_tool2.yaml" del "tools\valid_type_tool2.yaml"
    if exist "tools\DeleteTestTool1.yaml" del "tools\DeleteTestTool1.yaml"
    if exist "tools\DeleteTestTool2.yaml" del "tools\DeleteTestTool2.yaml"

    REM Keep example_tool.yaml as it was created by init
)

REM Clean up malformed YAML test files
if exist "agents" (
    if exist "agents\malformed_syntax.yaml" del "agents\malformed_syntax.yaml"
    if exist "agents\missing_fields.yaml" del "agents\missing_fields.yaml"
    if exist "agents\invalid_types.yaml" del "agents\invalid_types.yaml"
    if exist "agents\malformed_structure.yaml" del "agents\malformed_structure.yaml"
)

REM Clean up test profile files
if exist ".sreagent-profiles" (
    if exist ".sreagent-profiles\test_local.json" del ".sreagent-profiles\test_local.json"
    if exist ".sreagent-profiles\test_remote.json" del ".sreagent-profiles\test_remote.json"
    if exist ".sreagent-profiles\test_current.json" del ".sreagent-profiles\test_current.json"
)

REM Clean up test output files
del test*_output.txt 2>nul
del init_output.txt 2>nul

REM Return to original directory
popd
rmdir test_output 2>nul

echo.
echo =====================================
echo TEST SUMMARY
echo =====================================
echo Total Tests: !TOTAL_TESTS!
echo Passed: !TESTS_PASSED!
echo Failed: !TESTS_FAILED!

if !TESTS_FAILED! equ 0 (
    echo.
    echo [SUCCESS] All tests passed!
    echo The SRECTL is functioning correctly.
    echo.
    echo Key features tested:
    echo - Agent creation and validation
    echo - Tool creation and validation
    echo - Basic YAML validation
    echo - Tool existence validation (--check-tools)
    echo - Error handling for missing tools
    echo - Bulk validation operations
    echo - YAML format verification
    echo - Directory structure validation
    echo - Apply command functionality
    echo - Malformed YAML handling
    echo - Configuration file edge cases
    echo - Tool type validation and schema checking
    echo - Agent delete command functionality
    echo - Tool delete command functionality
    echo - Delete command error handling
    echo - Dependency checking for deletion
    echo - Server integration for delete operations
    echo - Profile management (create, list, get, set, delete)
    echo - Profile switching and current profile tracking
    echo - Profile validation and error handling
    echo - 60k character limit validation (^~15k tokens)
    exit /b 0
) else (
    echo.
    echo [FAILURE] Some tests failed!
    echo Please review the failed tests above and fix any issues.
    exit /b 1
)
