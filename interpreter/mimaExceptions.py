

class MimaIndexOutOfBound(Exception):
	def __init__(self, idx : int):
		self.idx = idx