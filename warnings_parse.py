from threading import Timer
import os


class RepeatTimer(Timer):
    def run(self):
        while not self.finished.wait(self.interval):
            self.function(*self.args, **self.kwargs)


connected_phrase = [
    "HostAsync - completed with HostResult = 0"
]

phrases = {
    "visible": "Publishing Visible match",
    "start": "APP -- Game Start",
    "stop": "APP -- Game Stop",
    "exit": "Datastore -- uninitialize complete",
    "map": "GameOptionsPanel::PopulateMapListInternal OnSelectMap(32) ((4p) Hydris Chasm)",
    "invisible": "WorldwidePartyService::SetMatchState - state 1 - updating server"
}

state = "CLOSED"
chosen_map = None
player_max = None

with os.open("warnings.txt", mode=os.O_SHLOCK|os.O_RDONLY) as logfile:
    monitor = None

    def task_monitor():
        global state
        global closen_map
        global player_max

        for line in logfile:
            match = line.strip().decode("utf-8-sig", "ignore")[15:]
            if len(line) > 15:
                if match == phrases["exit"]:
                    print("Game exited.")
                    monitor.cancel()

                if state == "CLOSED":
                    if match == phrases["visible"]:
                        print("Monitoring active lobby!")
                        state = "LOBBYOPEN"
                elif state == "LOBBYOPEN":
                    if match == phrases["start"]:
                        print("Game has started.")
                        state = "INGAME"
                    elif match == phrases["invisible"]:
                        print("Lobby has filled!")
                        state = "LOBBYFULL"
                    elif match == phrases["stop"]:
                        print("Lobby closed.")
                        state = "CLOSED"
                elif state == "LOBBYFULL":
                    if match == phrases["start"]:
                        print("Game has started.")
                        state = "INGAME"
                    elif match == phrases["visible"]:
                        print("Lobby has vacated!")
                        state = "LOBBYOPEN"
                    elif match == phrases["stop"]:
                        print("Lobby has closed.")
                        state = "CLOSED"
                elif state == "INGAME":
                    if match == phrases["stop"]:
                        print("Lobby has closed.")
                        state = "CLOSED"

    monitor = RepeatTimer(1, task_monitor)

    monitor.start()
    monitor.join()

sc.delete()
