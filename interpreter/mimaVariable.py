#!/usr/bin/env python3

from interpreter.mimaExceptions import MimaIndexOutOfBound

class Variable:
    # ONLY! uninitialized variables have value None
    def __init__(self, type : str, value=None):
        self.type = type
        self.value = value

    def __repr__(self):
        return repr((self.type, self.value))

class Array:

    def __init__(self, type: str, value=tuple):
        self.type = type
        self.value = value
        
    def __repr__(self):
        return repr((self.type, self.value))
    
    def __setitem__(self, idx, data):
        if self.outofbounds(idx):
            raise MimaIndexOutOfBound(idx)
        self.value[idx] = data

    def __getitem__(self, floor_number):
        if self.outofbounds(idx):
            raise MimaIndexOutOfBound(idx)
        return self.value[idx]

    def outofbounds(self, idx):
        return idx < 0 or idx >= len(self.value)