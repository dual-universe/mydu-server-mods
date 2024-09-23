# Proof of concept of engine rotation

This is just a proof of concept and not realy useable as is.



https://github.com/user-attachments/assets/d6c7a9d1-7baa-45c1-b875-de352a52853c



## Usage

Right-click on the engines you want rotation control for and hit "Engine->set as rotating".

To rotate right click anywhere and select "Activate rotation control".

A new empty notification square appears on the bottom-right of the screen. Click on it and a tiny scrollbar will appear.

Click the scrollbar to rotate the engines on the construct you are parented to.


## Usage by LUA from a control seat.

You will need 4 bound keyboard keys to lua option keys: two for rotation and
two for vertical throttle.

Be sure to link your engines to the control seat.

Here are the scripts for them (those go in system onActionStart filters):

### Rotate to horizontal

    if esetup == nil then
        esetup = 1
        eangle = 0
    end
    
    eangle = eangle - 10
    system.modAction("NQ.RotateEngine", 1000000, 0, 0, 0, tostring(eangle))
    -- force engines to recompute their tags
    for key, value in pairs(unit) do
        if type(value) == "table" and type(value.export) == "table" then
            if value.getThrust and value.getMaxThrust then
                value.setTags("dummy", false)
            end
        end
    end

### Rotate to vertical

    if esetup == nil then
        esetup = 1
        eangle = 0
    end
    
    eangle = eangle + 10
    system.modAction("NQ.RotateEngine", 1000000, 0, 0, 0, tostring(eangle))
    -- force engines to recompute their tags
    for key, value in pairs(unit) do
        if type(value) == "table" and type(value.export) == "table" then
            if value.getThrust and value.getMaxThrust then
                value.setTags("dummy", false)
            end
        end
    end

### Vertical throttle up

    if asetup == nil then
        asetup = 1
        vcmd = 0
    end
    vcmd = vcmd + 0.1
    if vcmd > 1 then
        vcmd = 1
    end
    Nav.axisCommandManager:setThrottleCommand(axisCommandId.vertical, vcmd)

### Vertical throttle down

    if asetup == nil then
        asetup = 1
        vcmd = 0
    end
    vcmd = vcmd - 0.1
    -- allow a bit of negative to be sure to be able to disable gravity compensator
    if vcmd < -0.1 then
        vcmd = -0.1
    end
    Nav.axisCommandManager:setThrottleCommand(axisCommandId.vertical, vcmd)
